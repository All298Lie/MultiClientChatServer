using System.Net;
using System.Net.Sockets;

namespace ChatClient
{
    internal static class ChatClient // ChatClient 클래스
    {
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
                    Console.WriteLine("서버 연결에 실패하였습니다. (10초 후 재시도)");

                    Thread.Sleep(10 * 1000); // 10(s) * 1000(ms)
                }
            } // while 문
            
            Console.WriteLine($"서버[{serverSock.RemoteEndPoint}]와의 연결에 성공했습니다.");

            // 5. 로그인

            // 6. 서버에게 통신을 받는 스레드

            // 7. 서버에 통신을 전송하는 스레드
        }
    }
}