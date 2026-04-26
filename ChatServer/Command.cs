using Newtonsoft.Json;
using ServerCore;
using System.Net.Sockets;
using System.Text;

namespace ChatServer
{
    internal static class Command
    {
        private static readonly object _lockObj = new object(); // lock 키워드용

        private static Dictionary<string, string> replyTargets = new Dictionary<string, string>();

        internal static void ListCommand(Socket clientSock) // 현재 접속 중인 유저 명단을 확인하는 명령어
        {
            lock (_lockObj)
            {
                List<string> users = ChatServer.GetConnectUserList();

                StringBuilder text = new StringBuilder();
                text.Append($"현재 접속 중인 유저 [{users.Count}명] :\n");

                // 현재 접속 중인 유저 닉네임을 알파벳 순으로 정렬
                List<string> sortedUsers = users.OrderBy(u => u, StringComparer.OrdinalIgnoreCase) // 대소문자 무시하고 알파벳순 정렬
                                                      .ThenBy(u => u, StringComparer.Ordinal) // 스펠링이 같으면 대문자 우선 정렬
                                                      .ToList();

                text.AppendJoin(", ", sortedUsers); // 정렬된 닉네임을 메세지로 추가

                S2C_Chat packet = new S2C_Chat("시스템", text.ToString());
                string json = JsonConvert.SerializeObject(packet);
                byte[] buffer = Encoding.UTF8.GetBytes(json);

                clientSock.Send(buffer);
            }
        }

        internal static void HelpCommand(Socket clientSock) // 사용 가능한 명령어를 알려주는 명령어
        {
            StringBuilder text = new StringBuilder();
            text.Append("사용 가능한 명령어를 확인합니다.\n\n");
            text.Append("= = = = = = 명령어 = = = = = =\n");
            text.Append("/exit - 서버와의 연결을 끊기. (/e, /ㄷ, /나가기)\n");
            text.Append("/help - 사용 가능한 명령어 확인 (/h, /ㅗ, /도움말)\n");
            text.Append("/list - 현재 접속 중인 유저 명단을 확인 (/l, /ㅣ, /목록)\n");
            text.Append("/reply <메세지> - 가장 최근에 대화한 대상에게만 보이는 메세지 전송 (/r, /ㄱ, /답장)\n");
            text.Append("/room - 방과 관련된 명령어 (/방)\n");
            text.Append("/whisper <대상> <메세지> - 대상만 보이는 메세지 전송 (/w, /ㅈ, /귓속말)\n");
            text.Append("= = = = = = = = = = = = = = =\n\n");

            S2C_Chat packet = new S2C_Chat("시스템", text.ToString());
            string json = JsonConvert.SerializeObject(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void WhisperCommand(Socket clientSock, string username, string[] args) // 대상에게 귓속말을 전송해주는 명령어
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
                    Socket? targetSock = ChatServer.GetConnectUserSocket(target);

                    if (targetSock == null) // 대상이 존재하지 않을 경우
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
                            targetSock.Send(buffer);
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

        internal static void ReplyCommand(Socket clientSock, string username, string[] args) // 가장 최근 대화를 주고받은 대상에게 귓속말을 전송해주는 명령어
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
                    else
                    {
                        string target = replyTargets[username];
                        Socket? targetSock = ChatServer.GetConnectUserSocket(target);

                        if (targetSock == null) // 최근에 대화한 대상을 찾을 수 없을 경우
                        {
                            packet = new S2C_Chat("시스템", "대상을 찾을 수 없습니다.");
                        }
                        else // 최근에 대화한 대상이 존재할 경우
                        {
                            // 1. 대상과 유저의 최근 대화대상 업데이트
                            replyTargets[target] = username;

                            string text = string.Join(" ", args);

                            // 2. 대상에게 귓속말 패킷 전송
                            packet = new S2C_Whisper(username, text, false);

                            json = JsonConvert.SerializeObject(packet);
                            buffer = Encoding.UTF8.GetBytes(json);

                            try
                            {
                                targetSock.Send(buffer);
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
                    }
                } // lock 키워드
            }

            json = JsonConvert.SerializeObject(packet);
            buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void UnknownCommand(Socket clientSock) // 잘못된 명령어
        {
            S2C_Chat packet = new S2C_Chat("시스템", "존재하지 않는 명령어입니다. (/h)");
            string json = JsonConvert.SerializeObject(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void RemoveReplyTarget(string username)
        {
            lock (_lockObj)
            {
                if (replyTargets.ContainsKey(username) == true)
                {
                    replyTargets.Remove(username);
                }
            }
        }
    }
}
