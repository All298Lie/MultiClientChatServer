using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServerCore;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ChatServer
{
    internal static class ChatServer // ChatServer 클래스
    {
        private static readonly object _lockObj = new object(); // lock 키워드용

        private static Dictionary<string, Socket> connectUsers = new Dictionary<string, Socket>();
        private static Dictionary<string, string> linkRoomUsers = new Dictionary<string, string>();

        public static void Main() // ChatServer 프로젝트 메인 함수
        {
            // 1. IP주소 가져오기
            string host = Dns.GetHostName(); // 로컬 호스트 이름을 가져옴
            IPHostEntry ipHost = Dns.GetHostEntry(host); // 호스트 이름을 통해 네트워크 정보를 가져옴
            IPAddress ipAddr = ipHost.AddressList[0]; // 네트워크가 가진 IP 주소 목록 중 첫번째 선택

            // 2. EndPoint 지정
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777); // IP주소와 포트(7777)로 EndPoint 생성

            // 3. 리스너 소켓 생성
            Socket listenSock = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp); // 소켓을 AddressFamily, SocketType, ProtocolType을 지정하여 생성

            // 4. 소켓 설정 및 대기
            listenSock.Bind(endPoint); // 소켓에 EndPoint(도착 지점) 부여
            listenSock.Listen(1000); // 클라이언트 연결 요청 대기 (1000 : 대기열의 최대길이)

            Console.WriteLine("통신 대기 시작");

            // 5. 클라이언트와의 연결 성공 시 쓰레드를 통해 통신 시작
            bool isListen = true;
            while (isListen == true)
            {
                Socket clientSock = listenSock.Accept();

                Thread thread = new Thread(() => HandleClient(clientSock));

                thread.Start();
            }
        }

        private static void HandleClient(Socket clientSock) // 클라이언트와 통신할때 사용하는 함수
        {
            // 0. 디버그
            int threadId = Thread.CurrentThread.ManagedThreadId;
            Console.WriteLine($"[Thread-{threadId}] 클라이언트와 통신할 스레드가 할당되었습니다.");

            using (clientSock) // 안전하게 통신을 종료하기 위한 using 키워드
            {
                string? username = null;

                // 1. 클라이언트와 통신
                try
                {
                    bool isOpened = true;
                    while (isOpened == true)
                    {
                        // 1. 클라이언트에게 패킷 받기
                        byte[] buffer = new byte[2048]; // 데이터를 받을 버퍼 생성
                        int receiveCount = clientSock.Receive(buffer); // 클라이언트 소켓에서 전송받은 크기
                        string data = Encoding.UTF8.GetString(buffer, 0, receiveCount);

                        // 2. Json 파일 형식으로 (임시)변환
                        JObject obj = JObject.Parse(data);
                        
                        try
                        {
                            int type = obj["Type"].Value<int>(); // 패킷 종류를 알기 위해 Type 부분 파싱

                            // 3. 패킷 종류에 따라 처리
                            switch ((PacketType)type)
                            {
                                case PacketType.C2S_Login: // 로그인 요청
                                    username = HandleLogin(clientSock, data, username); // 성공적으로 로그인하였을 경우, 닉네임 반환
                                    break;

                                case PacketType.C2S_Chat: // 채팅 전송
                                    HandleChat(username, data);
                                    break;

                                case PacketType.C2S_Cmd: // 명령어 입력
                                    HandleCommand(clientSock, username, data);
                                    break;

                                case PacketType.C2S_RoomCmd: // /방 명령어 입력
                                    HandleRoomCommand(clientSock, username, data);
                                    break;

                                default: // 잘못된 패킷
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            // int type = obj["Type"].Value<int>(); 부분에서 에러가 떠도 서버가 끊기지 않도록 무시
                        }

                    } // while 문
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"{username??"로그인 하지 않은 유저"} 님이 연결을 종료하셨습니다.");
                }

                // 2. 연결 종료
                if (username != null) // 로그인을 한 유저일 경우
                {
                    lock (_lockObj) // 로그아웃 처리
                    {
                        // a. 유저의 최근 답장 기록 삭제
                        Command.RemoveReplyTarget(username);

                        if (linkRoomUsers.ContainsKey(username) == true)
                        {
                            linkRoomUsers.Remove(username);
                        }

                        // b. 연결된 유저 명단에서 삭제
                        if (connectUsers.ContainsKey(username) == true)
                        {
                            connectUsers.Remove(username);
                        }
                    }

                    UpdateConnect(false, username); // 로그아웃 메세지 전달
                }
            } // using 문
        }

        private static string? HandleLogin(Socket clientSock, string data, string? currentUsername) // 로그인 요청을 처리하는 함수
        {
            // 로그인 요청으로 패킷 데이터 파싱
            C2S_Login? packet = JsonConvert.DeserializeObject<C2S_Login>(data);

            if (packet == null) return null;

            string username = packet.Username;
            string errorMessage = "";

            // 1. 이미 접속 중인지 확인
            if (currentUsername != null) // 이미 해당 클라이언트가 접속 중일 경우
            {
                errorMessage = "이미 접속 중입니다.";
            }
            // 2. 닉네임 길이 검사
            else if (username.Length == 0 || username.Length > 12) // 닉네임 길이가 잘못된 경우
            {
                errorMessage = "닉네임의 길이는 1~12자여야 합니다.";
            }
            // 3. 닉네임 문자 검사
            else if (username.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) == false)
            {
                errorMessage = "닉네임에는 대소문자와 숫자만 입력 가능합니다.";
            }
            // 4. 닉네임 중복 확인
            else
            {
                lock (_lockObj)
                {
                    if (connectUsers.ContainsKey(username) == true)
                    {
                        errorMessage = "이미 사용 중인 닉네임입니다.";
                    }
                    else
                    {
                        connectUsers.Add(username, clientSock);
                    }
                }
            }

            // 클라이언트에게 패킷 전송
            bool isSuccess = (errorMessage == "");
            S2C_LoginResult result = new S2C_LoginResult(isSuccess, errorMessage);

            string json = JsonConvert.SerializeObject(result);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            clientSock.Send(buffer);

            // 닉네임 반환 및 시스템 메세지 전송
            if (isSuccess == false) return null; // 로그인에 실패하였을 경우, null 반환

            // 로그인에 성공하였을 경우, 시스템 메세지 전송
            UpdateConnect(true, username); // 로그인 메세지 전달

            return username;
        }

        private static void HandleChat(string? username, string data) // 전송받은 채팅을 연결된 유저한테 전송하는 함수
        {
            // 1. 채팅 형식으로 패킷 데이터 파싱
            C2S_Chat? result = JsonConvert.DeserializeObject<C2S_Chat>(data);

            if (result == null || username == null) return; // 아닐 경우, 리턴

            string targetRoom = "";
            bool isRoomChat = false;

            lock (_lockObj)
            {
                if (linkRoomUsers.ContainsKey(username) == true)
                {
                    targetRoom = linkRoomUsers[username];
                    isRoomChat = true;
                }
            }

            // 2. 현재 메세지 대상이 전체인지, 방인지 확인
            if (isRoomChat == false)
            {
                BroadCast(username, result.Text);
            }
            else
            {
                RoomCast(targetRoom, username, result.Text);
            }
        }

        private static void HandleCommand(Socket clientSock, string? username, string data) // 입력한 명령어를 실행시켜주는 함수
        {
            // 1. 명령어 형식으로 패킷 데이터 파싱
            C2S_Cmd? result = JsonConvert.DeserializeObject<C2S_Cmd>(data);

            if (result == null || username == null) return; // 아닐 경우, 리턴

            // 2. 명령어 타입에 맞게 함수 실행
            switch (result.Command)
            {
                case "list": // /list, /l, /ㅣ, /목록 명령어
                case "l":
                case "ㅣ":
                case "목록":
                    Command.ListCommand(clientSock);
                    break;

                case "help": // /help, /h, /ㅗ, /도움말 명령어
                case "h":
                case "ㅗ":
                case "도움말":
                    Command.HelpCommand(clientSock);
                    break;

                case "whisper": // /whisper, /w, /ㅈ, /귓, /귓속말 명령어
                case "w":
                case "ㅈ":
                case "귓":
                case "귓속말":
                    Command.WhisperCommand(clientSock, username, result.Args);
                    break;

                case "reply": // /reply, /r, /ㄱ, /답장 명령어
                case "r":
                case "ㄱ":
                case "답장":
                    Command.ReplyCommand(clientSock, username, result.Args);
                    break;

                default:
                    Command.UnknownCommand(clientSock);
                    break;
            }
        }

        private static void HandleRoomCommand(Socket clientSock, string? username, string data) // /방 명령어 입력
        {
            // 1. 방 명령어 형식으로 패킷 데이터 파싱
            C2S_RoomCmd? result = JsonConvert.DeserializeObject<C2S_RoomCmd>(data);

            if (result == null || username == null) return; // 아닐 경우, 리턴

            // 2. 명령어 타입에 맞게 함수 실행
            switch ((RoomActionType)result.RoomActionType)
            {
                case RoomActionType.Help:
                    RoomCommand.HelpCommand(clientSock);
                    break;

                case RoomActionType.Accept:
                    RoomCommand.DecisionCommand(clientSock, username, true);
                    break;

                case RoomActionType.Deny:
                    RoomCommand.DecisionCommand(clientSock, username, false);
                    break;

                case RoomActionType.List:
                    RoomCommand.ListCommand(clientSock);
                    break;

                case RoomActionType.ToggleChat:
                    RoomCommand.ToggleChatCommand(clientSock, username, result.RoomName);
                    break;

                case RoomActionType.Chat:
                    RoomCommand.ChatCommand(clientSock, username, result.RoomName, result.Data);
                    break;

                case RoomActionType.Info:
                    RoomCommand.InfoCommand(clientSock, username, result.RoomName);
                    break;
                case RoomActionType.Create:
                    RoomCommand.CreateCommand(clientSock, username, result.RoomName);
                    break;

                case RoomActionType.Members:
                    RoomCommand.MembersCommand(clientSock, username, result.RoomName);
                    break;

                case RoomActionType.Join:
                    RoomCommand.JoinCommand(clientSock, username, result.RoomName);
                    break;

                case RoomActionType.Quit:
                    RoomCommand.QuitCommand(clientSock, username, result.RoomName);
                    break;

                case RoomActionType.Invite:
                    RoomCommand.InviteCommand(clientSock, username, result.RoomName, result.Data);
                    break;

                case RoomActionType.Kick:
                    RoomCommand.KickCommand(clientSock, username, result.RoomName, result.Data);
                    break;

                case RoomActionType.Privacy:
                    RoomCommand.PrivacyCommand(clientSock, username, result.RoomName, result.Data);
                    break;

                case RoomActionType.Delegate:
                    RoomCommand.DelegateCommand(clientSock, username, result.RoomName, result.Data);
                    break;

                case RoomActionType.SetTopic:
                    RoomCommand.SetTopicCommand(clientSock, username, result.RoomName, result.Data);
                    break;

                case RoomActionType.Rename:
                    RoomCommand.RenameCommand(clientSock, username, result.RoomName, result.Data);
                    break;
            }
        }

        private static void UpdateConnect(bool isLogin, string username) // 로그인/아웃과 관련된 내용을 모든 플레이어에게 전달하는 함수
        {
            lock (_lockObj)
            {
                string message = $"{username}님이 {(isLogin == true ? "입장" : "퇴장")}했습니다. [현재 인원 수 : {connectUsers.Count}명]";

                BroadCast("시스템", message); // 모든 유저에게 입/퇴장 메세지 전송
            }
        }

        private static void BroadCast(string sender, string message) // 서버와 연결된 모든 클라이언트에게 메세지를 전송하는 함수
        {
            // 1. 채팅 형식으로 전달
            S2C_Chat result = new S2C_Chat(sender, message);

            string json = JsonConvert.SerializeObject(result);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            // 2. 연결된 모든 유저에게 패킷 전송
            lock (_lockObj)
            {
                foreach (Socket sock in connectUsers.Values)
                {
                    try
                    {
                        sock.Send(buffer);
                    }
                    catch (SocketException ex)
                    {
                        // 전송 실패(도중에 연결 끊김)
                    }
                }
            }

            ChatLogger.SaveLog($"[{sender}] {message}"); // 서버에 메세지 로그 기록
        }
    
        internal static void RoomCast(string roomName, string sender, string message)
        {
            // 1. 방 채팅 형식으로 전달
            S2C_RoomChat result = new S2C_RoomChat(roomName, sender, message);

            string json = JsonConvert.SerializeObject(result);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            List<string> members = RoomCommand.GetRoomMembers(roomName);
            lock (_lockObj)
            {
                // 2. 연결된 모든 멤버에게 패킷 전송
                foreach (string member in members)
                {
                    Socket? memberSock = GetConnectUserSocket(member);

                    if (memberSock == null) continue;

                    try
                    {
                        memberSock.Send(buffer);
                    }
                    catch (SocketException ex)
                    {
                        // 전송 실패(도중에 연결 끊김)
                    }
                }
            } // lock 키워드

            ChatLogger.SaveLog($"[{roomName}] [{sender}] {message}");
        }

        internal static Socket? GetConnectUserSocket(string username)
        {
            lock (_lockObj)
            {
                if (connectUsers.ContainsKey(username) == false) return null;

                return connectUsers[username];
            }
        }

        internal static List<string> GetConnectUserList()
        {
            lock (_lockObj)
            {
                return connectUsers.Keys.ToList();
            }
        }

        internal static bool ToggleLinkRoomUsers(string username, string roomName)
        {
            lock (_lockObj)
            {
                if (linkRoomUsers.ContainsKey(username) == true)
                {
                    if (linkRoomUsers[username] == roomName) // 이미 해당 방 채팅 토글 상태였을 경우
                    {
                        linkRoomUsers.Remove(username);

                        return false;
                    }
                }

                // 그 외의 상태였을 경우(전체 채팅 / 다른 방 채팅 토글)

                linkRoomUsers[username] = roomName;

                return true;
            }
        }

        internal static void RenameLinkRoomUsers(string oldname, string newname)
        {
            lock (_lockObj)
            {
                List<string> users = linkRoomUsers.Keys.ToList();

                foreach (string user in users)
                {
                    if (linkRoomUsers[user].Equals(oldname) == true)
                    {
                        linkRoomUsers[user] = newname;
                    }
                }
            } // lock 키워드
        }
    }
}