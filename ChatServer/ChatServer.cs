using System.Net;
using System.Net.Sockets;

namespace ChatServer
{
    internal static class ChatServer // ChatServer 클래스
    {
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

        }
    }
}