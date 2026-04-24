namespace ServerCore
{
    public enum PacketType // 패킷 타입 열거형
    {
        None = 0, // 잘못된 패킷 종류

        C2S_Login = 1, // 클라이언트 -> 서버 : 로그인 요청
        S2C_LoginResult = 2, // 서버 -> 클라이언트 : 로그인 요청 결과

        C2S_Chat = 3, // 클라이언트 -> 서버 : 채팅 전송
        S2C_Chat = 4, // 서버 -> 클라이언트 : 채팅 전달

        C2S_Cmd = 5, // 클라이언트 -> 서버 : 명령어 입력
        S2C_Whisper = 6, // 서버 -> 클라이언트 : 귓속말 전달
        S2C_Room = 7 // 서버 -> 클라이언트 : 방관련 전달
        
    }

    public abstract class Packet // 패킷 추상 클래스.
    {
        public int Type { get; protected set; } // 패킷 종류
    }

    public class C2S_Login : Packet // 로그인 요청 패킷
    {
        public string Username { get; private set; } // 유저 닉네임.

        public C2S_Login(string username)
        {
            this.Type = (int)PacketType.C2S_Login;

            this.Username = username;
        }
    }

    public class S2C_LoginResult : Packet // 로그인 요청 결과 패킷
    {
        public string Reason { get; private set; } // 사유(로그인 실패일 경우 사용)

        public bool SuccessLogin { get; private set; } // 로그인 성공 여부

        public S2C_LoginResult(bool successLogin, string reason)
        {
            this.Type = (int)PacketType.S2C_LoginResult;

            this.SuccessLogin = successLogin;
            this.Reason = reason;
        }
    }

    public class C2S_Chat : Packet // 채팅 전송 패킷
    {
        public string Text { get; private set; } // 내용

        public C2S_Chat(string text)
        {
            this.Type = (int)PacketType.C2S_Chat;

            this.Text = text;
        }
    }

    public class S2C_Chat : Packet // 채팅 전달 패킷
    {
        public string Sender { get; private set; } // 보낸 사람
        public string Text { get; private set; } // 내용

        public S2C_Chat(string sender, string text)
        {
            this.Type = (int)PacketType.S2C_Chat;

            this.Sender = sender;
            this.Text = text;
        }
    }

    public class C2S_Cmd : Packet // 명령어 입력 패킷
    {
        public string Command { get; private set; } // 명령어
        public string[] Args { get; private set; } // 명령어의 인자

        public C2S_Cmd(string command, string[] args)
        {
            this.Type = (int)PacketType.C2S_Cmd;

            this.Command = command;
            this.Args = args;
        }
    }

    public class S2C_Whisper : Packet // 귓속말 전달 패킷
    {
        public string Target { get; private set; } // 보낸 사람 혹은 받은 사람
        public string Text { get; private set; } // 메세지
        public bool IsSelf { get; private set; } // 전달 하는지, 받는지 여부

        public S2C_Whisper(string target, string text, bool isSelf)
        {
            this.Type = (int)PacketType.S2C_Whisper;

            this.Target = target;
            this.Text = text;
            this.IsSelf = isSelf;
        }
    }
}
