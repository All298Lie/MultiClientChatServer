using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

using ServerCore;

namespace ChatServer
{
    internal static class ChatServer // ChatServer 클래스
    {
        private static readonly object _lockObj = new object(); // lock 키워드용

        private static Dictionary<string, Socket> connectUsers = new Dictionary<string, Socket>();
        private static Dictionary<string, string> replyTargets = new Dictionary<string, string>();

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
                        byte[] buffer = new byte[1024]; // 데이터를 받을 버퍼 생성
                        int receiveCount = clientSock.Receive(buffer); // 클라이언트 소켓에서 전송받은 크기
                        string data = Encoding.UTF8.GetString(buffer, 0, receiveCount);

                        // 2. Json 파일 형식으로 (임시)변환
                        JObject obj = JObject.Parse(data);
                        int type = obj["Type"].Value<int>(); // 패킷 종류를 알기 위해 Type 부분 파싱

                        // 3. 패킷 종류에 따라 처리
                        switch ((PacketType) type)
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

                            default: // 잘못된 패킷
                                break;
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
                        if (connectUsers.ContainsKey(username) == true)
                        {
                            connectUsers.Remove(username);
                        }

                        if (replyTargets.ContainsKey(username) == true)
                        {
                            replyTargets.Remove(username);
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

            // 2. 받은 메세지를 모든 유저에게 전송
            BroadCast(username, result.Text);
        }

        private static void HandleCommand(Socket clientSock, string? username, string data) // 입력한 명령어를 실행시켜주는 함수
        {
            // 1. 명령어 형식으로 패킷 데이터 파싱
            C2S_Cmd? result = JsonConvert.DeserializeObject<C2S_Cmd>(data);

            if (result == null || username == null) return; // 아닐 경우, 리턴

            switch (result.Command)
            {
                case "list": // /list, /l, /ㅣ, /목록 명령어
                case "l":
                case "ㅣ":
                case "목록":
                    ListCommand(clientSock);
                    break;

                case "help": // /help, /h, /ㅗ, /도움말 명령어
                case "h":
                case "ㅗ":
                case "도움말":
                    HelpCommand(clientSock);
                    break;

                case "whisper": // /whisper, /w, /ㅈ, /귓, /귓속말 명령어
                case "w":
                case "ㅈ":
                case "귓":
                case "귓속말":
                    WhisperCommand(clientSock, username, result.Args);
                    break;

                case "reply": // /reply, /r, /ㄱ, /답장 명령어
                case "r":
                case "ㄱ":
                case "답장":
                    ReplyCommand(clientSock, username, result.Args);
                    break;

                default:
                    UnknownCommand(clientSock);
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

        private static void ListCommand(Socket clientSock) // 현재 접속 중인 유저 명단을 확인하는 명령어
        {
            lock (_lockObj)
            {
                StringBuilder text = new StringBuilder();
                text.Append($"현재 접속 중인 유저 [{connectUsers.Count}명] :\n");

                // 현재 접속 중인 유저 닉네임을 알파벳 순으로 정렬
                List<string> sortedUsers = connectUsers.Keys.OrderBy(u => u, StringComparer.OrdinalIgnoreCase) // 대소문자 무시하고 알파벳순 정렬
                                                      .ThenBy(u => u, StringComparer.Ordinal) // 스펠링이 같으면 대문자 우선 정렬
                                                      .ToList();

                text.AppendJoin(", ", sortedUsers); // 정렬된 닉네임을 메세지로 추가

                S2C_Chat packet = new S2C_Chat("시스템", text.ToString());
                string json = JsonConvert.SerializeObject(packet);
                byte[] buffer = Encoding.UTF8.GetBytes(json);

                clientSock.Send(buffer);
            }
        }

        private static void HelpCommand(Socket clientSock) // 사용 가능한 명령어를 알려주는 명령어
        {
            StringBuilder text = new StringBuilder();
            text.Append("사용 가능한 명령어를 확인합니다.\n\n");
            text.Append("= = = = = = 명령어 = = = = = =\n");
            text.Append("/exit - 서버와의 연결을 끊습니다. (/e, /ㄷ, /나가기)\n");
            text.Append("/help - 사용 가능한 명령어 확인 (/h, /ㅗ, /도움말)\n");
            text.Append("/list - 현재 접속 중인 유저 명단을 확인 (/l, /ㅣ, /목록)\n");
            text.Append("/reply <메세지> - 가장 최근에 대화한 대상에게만 보이는 메세지 전송 (/r, /ㄱ, /답장)\n");
            text.Append("/whisper <대상> <메세지> - 대상만 보이는 메세지 전송 (/w, /ㅈ, /귓속말)\n");
            text.Append("= = = = = = = = = = = = = = =\n\n");

            S2C_Chat packet = new S2C_Chat("시스템", text.ToString());
            string json = JsonConvert.SerializeObject(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        private static void WhisperCommand(Socket clientSock, string username, string[] args) // 대상에게 귓속말을 전송해주는 명령어
        {
            Packet packet;
            string json;
            byte[] buffer;

            if (args.Length <= 1) // 인자가 <대상>, <메시지>만큼 존재하지 않을 경우
            {
                packet = new S2C_Chat("시스템", "잘못된 명령어입니다. (/w <대상> <메세지>)");
            }
            else // 인자가 정상적으로 존재할 경우
            {
                string target = args[0]; // 대화 대상

                lock (_lockObj)
                {
                    if (connectUsers.ContainsKey(target) == false) // 대상이 존재하지 않을 경우
                    {
                        packet = new S2C_Chat("시스템", "대상을 찾을 수 없습니다.");
                    }
                    else // 대상이 존재할 경우
                    {
                        // 1. 대상과 유저의 최근 대화 대상 업데이트
                        replyTargets[username] = target;
                        replyTargets[target] = username;

                        string text = string.Join(" ", args.Skip(1));

                        // 2. 대상에게 귓속말 패킷 전송

                        packet = new S2C_Whisper(username, text, false);

                        json = JsonConvert.SerializeObject(packet);
                        buffer = Encoding.UTF8.GetBytes(json);

                        try
                        {
                            connectUsers[target].Send(buffer);
                        }
                        catch (SocketException ex)
                        {
                            // 대상이 도중에 접속이 끊겨 생긴 오류로 유저가 쓰레드에서 탈출되지 않도록 방지
                        }

                        // 3. 자신에게 귓속말 패킷 전송
                        packet = new S2C_Whisper(target, text, true);

                        // 4. 서버 로그
                        ChatLogger.SaveLog($"[{username} >> {target}] {text}");
                    }
                } // lock 키워드
            }

            json = JsonConvert.SerializeObject(packet);
            buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        private static void ReplyCommand(Socket clientSock, string username, string[] args) // 가장 최근 대화를 주고받은 대상에게 귓속말을 전송해주는 명령어
        {
            Packet packet;
            string json;
            byte[] buffer;

            if (args.Length == 0) // 인자가 존재하지 않을 경우
            {
                packet = new S2C_Chat("시스템", "잘못된 명령어입니다. (/r <메세지>)");
            }
            else
            {
                lock (_lockObj)
                {
                    if (replyTargets.ContainsKey(username) == false) // 유저와 최근에 대화한 대상이 없을 경우
                    {
                        packet = new S2C_Chat("시스템", "최근에 대화한 유저가 없습니다.");
                    }
                    else if (connectUsers.ContainsKey(replyTargets[username]) == false) // 최근에 대화한 대상을 찾을 수 없을 경우
                    {
                        packet = new S2C_Chat("시스템", "대상을 찾을 수 없습니다.");
                    }
                    else // 최근에 대화한 대상이 존재할 경우
                    {
                        string target = replyTargets[username];

                        // 1. 대상과 유저의 최근 대화대상 업데이트
                        replyTargets[target] = username;

                        string text = string.Join(" ", args);

                        // 2. 대상에게 귓속말 패킷 전송
                        packet = new S2C_Whisper(username, text, false);

                        json = JsonConvert.SerializeObject(packet);
                        buffer = Encoding.UTF8.GetBytes(json);

                        try
                        {
                            connectUsers[target].Send(buffer);
                        }
                        catch (SocketException ex)
                        {
                            // 대상이 도중에 접속이 끊겨 생긴 오류로 유저가 쓰레드에서 탈출되지 않도록 방지
                        }

                        // 3. 자신에게 귓속말 패킷 전송
                        packet = new S2C_Whisper(target, text, true);

                        // 4. 서버 로그
                        ChatLogger.SaveLog($"[{username} >> {target}] {text}");
                    }
                } // lock 키워드
            }

            json = JsonConvert.SerializeObject(packet);
            buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        private static void UnknownCommand(Socket clientSock) // 잘못된 명령어
        {
            S2C_Chat packet = new S2C_Chat("시스템", "존재하지 않는 명령어입니다. (/h)");
            string json = JsonConvert.SerializeObject(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }
    }
}