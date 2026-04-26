using System.Text.Json.Serialization;

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

        C2S_RoomCmd = 7, // 클라이언트 -> 서버 : 방 명령어 입력
        S2C_RoomChat = 8 // 서버 -> 클라이언트 : 방 전용 채팅 전달
    }

    public enum RoomActionType // 방 명령어 행동 타입 열거형
    {
        Help = 0, // /방 도움말

        Accept = 1, // /방 수락
        Deny = 2, // /방 거절

        List = 3, // /방 목록

        ToggleChat = 4, // /방 <이름>
        Chat = 5, // /방 <이름> <메세지>

        Info = 6,
        Create = 7, // /방 <이름> 생성
        Members = 8, // /방 <이름> 명단
        Join = 9, // /방 <이름> 참가
        Quit = 10, // /방 <이름> 탈퇴

        Invite = 11, // /방 <이름> 초대 <유저>
        Kick = 12, // /방 <이름> 추방 <유저>
        Privacy = 13, // /방 <이름> 공개설정 [공개/비공개]
        Delegate = 14, // /방 <이름> 방장위임 <유저>
        SetTopic = 15, // /방 <이름> 설명 <내용>
        Rename = 16 // /방 <이름> 이름변경 <이름2>
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

    public class C2S_RoomCmd : Packet // 방 명령어 패킷
    {
        public int RoomActionType; // 명령어 종류 구분용 열거형
        public string RoomName; // 방 이름 (필요 없을 시 "" 공백으로 처리)

        public string Data; // 그 외에 필요 정보를 담는 곳 (필요 없을 시 "" 공백으로 처리)

        [JsonConstructor] // 생성자 여러개일 때, Json 파싱 시 해당 생성자를 사용하도록 설정
        public C2S_RoomCmd()
        {
            // Json 파싱 전용 생성자
        }

        public C2S_RoomCmd(RoomActionType roomActionType, string roomName, string data)
        {
            this.Type = (int)PacketType.C2S_RoomCmd;

            this.RoomActionType = (int)roomActionType;

            this.RoomName = roomName ?? "";
            this.Data = data ?? "";
        }

        public C2S_RoomCmd(RoomActionType roomActionType, string roomName) : this(roomActionType, roomName, "") { }

        public C2S_RoomCmd(RoomActionType roomActionType) : this(roomActionType, "", "") { }
    }

    public class S2C_RoomChat : Packet
    {
        public string RoomName { get; private set; }

        public string Sender { get; private set; }
        public string Text { get; private set; }

        public S2C_RoomChat(string roomName, string sender, string text)
        {
            this.Type = (int)PacketType.S2C_RoomChat;

            this.RoomName = roomName;

            this.Sender = sender;
            this.Text = text;
        }
    }
}
