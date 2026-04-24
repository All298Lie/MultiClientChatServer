using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

using ServerCore;

namespace ChatClient
{
    internal static class ChatClient // ChatClient 클래스
    {
        private static bool isOpened;

        public static void Main() // ChatClient 프로젝트 메인 함수
        {
            // 1. IP주소 가져오기
            string host = Dns.GetHostName(); // 로컬 호스트 이름을 가져옴
            IPHostEntry ipHost = Dns.GetHostEntry(host); // 호스트 이름 또는 IP 주소를 통해 IPHostEntry를 리턴받음
            IPAddress ipAddr = ipHost.AddressList[0]; // ipHost의 속성에서 IPAddress를 가져옴

            // 2. EndPoint 지정
            IPEndPoint endPoint = new IPEndPoint(ipAddr, 7777); // 네트워크 끝 점을 IP주소와 포트(7777)로 생성

            // 3. 통신용 소켓 생성
            Socket serverSock = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp); // 소켓을 AddressFamily, SocketType, ProtocolType을 지정하여 생성

            // 4. 서버에 연결 시도
            bool isConnect = false;
            while (isConnect == false) // 서버를 연결할때까지 반복
            {
                try
                {
                    Console.WriteLine("서버와 연결 시도 중...");
                    serverSock.Connect(endPoint); // 연결 시도. 연결하지 못할 경우 해당 함수에서 SocketException 에러를 반환

                    isConnect = true;
                }
                catch (SocketException ex) // 연결하지 못했을 경우
                {
                    Console.WriteLine("서버 연결에 실패하였습니다. (10초 후 재시도)\n");

                    Thread.Sleep(10 * 1000); // 10(s) * 1000(ms)
                }
            } // while 문

            isOpened = true;
            Console.WriteLine($"서버[{serverSock.RemoteEndPoint}]와의 연결에 성공했습니다.");

            using (serverSock) // 안전하게 통신을 종료하기 위한 using 키워드
            {
                try
                {
                    // 5. 로그인
                    bool successLogin = false;
                    while (successLogin == false)
                    {
                        Console.WriteLine("로그인에 사용할 닉네임을 입력해주세요. (문자 길이 1~12자 / 대소문자 및 숫자만 사용 가능)");
                        Console.Write("사용할 닉네임 : ");

                        string? strLine = Console.ReadLine();

                        // 입력한 채팅 부분을 덮어서 지우기
                        int targetLine = Console.CursorTop - 1;
                        Console.SetCursorPosition(0, targetLine);
                        Console.Write(new string(' ', Console.WindowWidth - 1));
                        Console.SetCursorPosition(0, targetLine);

                        if (strLine == null || strLine == "")
                        {
                            Console.WriteLine("닉네임을 입력해주세요.\n");

                            continue;
                        }

                        C2S_Login login = new C2S_Login(strLine);

                        // 서버로 로그인 요청
                        string json = JsonConvert.SerializeObject(login);
                        byte[] buffer = Encoding.UTF8.GetBytes(json);
                        serverSock.Send(buffer);

                        // 서버에게 로그인 요청 결과 받기
                        byte[] receiveBuffer = new byte[1024];
                        int receiveCount = serverSock.Receive(receiveBuffer);
                        string data = Encoding.UTF8.GetString(receiveBuffer, 0, receiveCount);

                        S2C_LoginResult? result = JsonConvert.DeserializeObject<S2C_LoginResult>(data);

                        if (result != null)
                        {
                            successLogin = result.SuccessLogin;

                            if (successLogin == false)
                            {
                                Console.WriteLine($"로그인에 실패하였습니다. \n사유 : {result.Reason}\n");
                            }
                            else
                            {
                                Console.WriteLine("로그인에 성공하였습니다.\n");
                            }
                        }
                    } // while 문
                }
                catch (SocketException ex) // 도중에 연결이 끊겼을 경우
                {
                    Console.WriteLine("서버와의 연결이 끊겼습니다.");
                }

                // 6. 서버에게 통신을 받는 스레드
                Thread receiveThread = new Thread(() => ReceivePacket(serverSock));
                receiveThread.Start();

                // 7. 서버에 통신을 전송
                SendPacket(serverSock);
                
            } // using 문
        }

        private static void ReceivePacket(Socket serverSock) // 통신을 받는 함수
        {
            try
            {
                while (isOpened == true)
                {
                    // 1. 서버에게 패킷 받기
                    byte[] buffer = new byte[1024];
                    int receiveCount = serverSock.Receive(buffer);
                    string data = Encoding.UTF8.GetString(buffer, 0, receiveCount);

                    // 2. Json 파일 형식으로 (임시)변환
                    JObject obj = JObject.Parse(data);
                    int type = obj["Type"].Value<int>(); // 패킷 종류를 알기 위해 Type 부분 파싱

                    // 3. 패킷 종류에 따라 처리
                    switch ((PacketType)type)
                    {
                        case PacketType.S2C_Chat: // 채팅 전달
                            HandleChat(data);
                            break;

                        case PacketType.S2C_Whisper: // 귓속말 전달
                            HandleWhisper(data);
                            break;

                        default:
                            break;
                    }
                } // while 문
            }
            catch (SocketException ex)
            {
                Console.WriteLine("서버와의 연결이 끊겼습니다.");
            }
        }

        private static void SendPacket(Socket serverSock) // 통신을 보내는 함수
        {
            try
            {
                while (isOpened == true)
                {
                    // 1. 입력 처리
                    string? strLine = Console.ReadLine();

                    // 입력한 채팅 부분을 덮어서 지우기
                    int targetLine = Console.CursorTop - 1;
                    Console.SetCursorPosition(0, targetLine);
                    Console.Write(new string(' ', Console.WindowWidth - 1));
                    Console.SetCursorPosition(0, targetLine);

                    if (strLine == null || strLine == "") continue;

                    // 2. 패킷화
                    Packet packet;
                    if (strLine.StartsWith('/') == true) // 명령어를 입력받았을 경우
                    {
                        if (strLine.Length > 1 && strLine[1] == ' ')
                        {
                            Console.WriteLine("[시스템] 잘못된 명령어입니다. (/h)");

                            continue;
                        }

                        if (strLine.StartsWith("/exit") == true || strLine.StartsWith("/e") == true || strLine.StartsWith("/ㄷ") == true || strLine.StartsWith("/나가기") == true) // 나가기 명령어일 경우, 탈출
                        {
                            isOpened = false;
                            break;
                        }

                        string[] parts = strLine.Substring(1).Split(' ', StringSplitOptions.RemoveEmptyEntries);

                        string command = parts[0]; // '/'를 제외한 명령어 부분
                        string[] args = parts.Skip(1).ToArray(); // 명령어 부분을 제외한 나머지 인자 부분

                        packet = new C2S_Cmd(command, args);
                    }
                    else // 채팅을 입력했을 경우
                    {
                        packet = new C2S_Chat(strLine);
                    }

                    // 3. 서버에 패킷 전송
                    string json = JsonConvert.SerializeObject(packet);
                    byte[] buffer = Encoding.UTF8.GetBytes(json);
                    serverSock.Send(buffer);
                } // while 문
            }
            catch (SocketException ex)
            {
                Console.WriteLine("서버와의 연결이 끊겼습니다.");
            }
        }

        private static void HandleChat(string data) // 전달받은 채팅을 출력해주는 함수
        {
            // 1. 패킷 파싱
            S2C_Chat? result = JsonConvert.DeserializeObject<S2C_Chat>(data);

            if (result == null) return;

            // 2. 채팅 출력
            Console.WriteLine($"[{result.Sender}] {result.Text}");
        }

        private static void HandleWhisper(string data) // 전달받은 귓속말을 출력해주는 함수
        {
            // 1. 패킷 파싱
            S2C_Whisper? result = JsonConvert.DeserializeObject<S2C_Whisper>(data);

            if (result == null) return;

            // 2. 귓속말 출력
            Console.WriteLine($"[귓속말] {result.Target} {(result.IsSelf == true ? "<<" : ">>")} {result.Text}");
        }
    }
}