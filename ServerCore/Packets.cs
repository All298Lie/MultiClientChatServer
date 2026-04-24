namespace ServerCore
{
    public enum PacketType // 패킷 타입 열거형
    {
        None = 0, // 잘못된 패킷 종류
        C2S_Login = 1, // 클라이언트 -> 서버 : 로그인 요청
        S2C_LoginResult = 2 // 서버 -> 클라이언트 : 로그인 요청 결과
    }

    public abstract class Packet
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
        public string Username { get; private set; } // 유저 닉네임
        public string Reason { get; private set; } // 사유(로그인 실패일 경우 사용)

        public bool SucessLogin { get; private set; } // 로그인 성공 여부

        public S2C_LoginResult(string username, bool sucessLogin, string reason)
        {
            this.Type = (int)PacketType.S2C_LoginResult;

            this.Username = username;

            this.SucessLogin = sucessLogin;
            this.Reason = reason;
        }
    }
}
