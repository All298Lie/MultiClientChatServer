using Newtonsoft.Json;
using System.Net.Sockets;
using System.Text;

using ServerCore;

namespace ChatServer
{
    internal class RoomCommand
    {
        private static readonly object _lockObj = new object(); // lock 키워드용

        private static Dictionary<string, Room> roomList = new Dictionary<string, Room>(); // 방 목록 <방이름, 방>
        private static Dictionary<string, string> recentInvite = new Dictionary<string, string>(); // 최근 초대를 기록 <유저, 방이름>

        internal static void HelpCommand(Socket clientSock) // /방 도움말 명령어
        {
            StringBuilder text = new StringBuilder();
            text.Append("사용 가능한 방 관련 명령어를 확인합니다.\n\n");
            text.Append("= = = = = = 명령어 = = = = = =\n");
            text.Append("/room help - 방 관련 도움말을 확인합니다. (/방 도움말)\n");
            text.Append("/room [accept/deny] - 최근 자신에게 온 방 초대를 수락/거절합니다. (/방 [수락/거절])\n");
            text.Append("/room list - 현재 공개된 방 명단을 확인합니다. (/방 목록)\n");
            text.Append('\n');
            text.Append("/room <name> - 현재 채팅 상태를 <이름> 방으로 토글합니다.(재입력 시 전체 채팅으로 전환) (/방 <이름>)\n");
            text.Append("/room <name> <message> - <이름> 방에 채팅을 입력합니다. (/방 <이름> <메세지>)\n");
            text.Append('\n');
            text.Append("/room <name> info - <이름> 방 정보를 확인합니다. (맴버 전용) (/방 <이름> 정보)\n");
            text.Append("/room <name> create - <이름> 방을 생성합니다. (/방 <이름> 생성)\n");
            text.Append("/room <name> members - <이름> 방의 인원을 확인합니다. (/방 <이름> 명단)\n");
            text.Append("/room <name> join - <이름> 방에 참여합니다. (공개방 전용) (/방 <이름> 참가)\n");
            text.Append("/room <name> quit - <이름> 방에서 퇴장합니다. (맴버 전용) (/방 <이름> 나가기)\n");
            text.Append('\n');
            text.Append("/room <name> invite <user> - <이름> 방에 <유저>에게 초대요청을 보냅니다. (방장 전용) (/방 <이름> 초대 <유저>)\n");
            text.Append("/room <name> kick <user> - <이름> 방에서 <유저>를 추방합니다. (방장 전용) (/방 <이름> 추방 <유저>)\n");
            text.Append("/room <name> privacy [public/private] - <이름> 방의 공개여부를 설정합니다. (방장 전용) (/방 <이름> 공개설정 [공개/비공개])\n");
            text.Append("/room <name> delegate <user> - <이름> 방의 방장을 <유저>에게 위임합니다. (방장 전용) (/방 <이름> 방장위임 <유저>)\n");
            text.Append("/room <name> settopic <topic> - '/방 목록'에서 뜰 <이름> 방 설명을 설정합니다.(미입력 시 초기화) (방장 전용) (/방 <이름> 설명 <내용>)\n");
            text.Append("/room <name> rename <new name> - <새이름>으로 방 이름을 변경합니다. (방장 전용) (/방 <이름> 이름변경 <새이름>)\n");
            text.Append("= = = = = = = = = = = = = = =\n\n");

            S2C_Chat packet = new S2C_Chat("시스템", text.ToString());
            string json = JsonConvert.SerializeObject(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void DecisionCommand(Socket clientSock, string? username, bool isAccept) // /방 [수락/거절]
        {
            S2C_Chat packet;

            lock (_lockObj)
            {
                // 1. 유저가 초대받은 기록이 있는지 확인
                if (username == null || recentInvite.ContainsKey(username) == false)
                {
                    packet = new S2C_Chat("시스템", "초대받은 기록이 존재하지 않습니다.");
                }
                else
                {
                    // 2. 해당 방이 존재하는지 확인
                    string roomName = recentInvite[username];
                    if (roomList.ContainsKey(roomName) == false)
                    {
                        packet = new S2C_Chat("시스템", "방이 존재하지 않습니다.");
                    }

                    // 3. 수락/거절 처리
                    recentInvite.Remove(username); // 초대받은 기록 삭제

                    if (isAccept == true) // 수락일 경우, 방에 입장
                    {
                        roomList[roomName].JoinMember(username);
                    }

                    packet = new S2C_Chat("시스템", $"{roomName} 방의 초대요청을 {(isAccept == true ? "수락" : "거절")}하셨습니다.");
                }

            } // lock 키워드

            string json = JsonConvert.SerializeObject(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }
        
        internal static void ListCommand(Socket clientSock) // /방 목록
        {
            StringBuilder text = new StringBuilder();
            text.Append("공개된 방 목록을 확인합니다.\n\n");
            text.Append("= = = = = = 방 목록 = = = = = =\n");

            lock (_lockObj)
            {
                foreach (string roomName in roomList.Keys)
                {
                    if (roomList.ContainsKey(roomName) == false) continue;

                    Room room = roomList[roomName];
                    if (room.IsPublic == false) continue;

                    text.Append($"{roomName} : {(room.Topic != "" ? room.Topic : "설명 없음")}\n");
                }
            }

            text.Append("= = = = = = = = = = = = = = =\n\n");

            S2C_Chat packet = new S2C_Chat("시스템", text.ToString());
            string json = JsonConvert.SerializeObject(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void ToggleChatCommand(Socket clientSock, string username, string roomName) // /방 <이름>
        {
            S2C_Chat packet;

            bool isPossible = true;
            lock (_lockObj)
            {
                // 1. 방에 존재하는지, 참여하고 있는지 확인
                if (roomList.ContainsKey(roomName) == false || roomList[roomName].Members.Contains(username) == false)
                {
                    isPossible = false;
                }
            }

            if (isPossible == true)
            {
                bool isToggle = ChatServer.ToggleLinkRoomUsers(username, roomName);

                packet = new S2C_Chat("시스템", $"방 채팅이 토글되었습니다. 상태 : {(isToggle == true ? $"{roomName} 채팅" : "전체 채팅")}");
            }
            else
            {
                packet = new S2C_Chat("시스템", "해당 방에 참여하고 있지 않습니다.");
            }

                string json = JsonConvert.SerializeObject(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void ChatCommand(Socket clientSock, string username, string roomName, string data) // /방 <이름> <메세지>
        {
            lock (_lockObj)
            {
                // 1. 방에 존재하는지, 참여하고 있는지 확인
                if (roomList.ContainsKey(roomName) == false || roomList[roomName].Members.Contains(username) == false)
                {
                    S2C_Chat packet = new S2C_Chat("시스템", "해당 방에 참여하고 있지 않습니다.");

                    string json = JsonConvert.SerializeObject(packet);
                    byte[] buffer = Encoding.UTF8.GetBytes(json);

                    clientSock.Send(buffer);

                    return;
                }
            }

            ChatServer.RoomCast(roomName, username, data); // 방 채팅
        }

        internal static void InfoCommand(Socket clientSock, string username, string roomName) // /방 <이름> 정보
        {
            S2C_Chat packet;

            lock (_lockObj)
            {
                // 1. 방에 존재하는지, 참여하고 있는지 확인
                if (roomList.ContainsKey(roomName) == false || roomList[roomName].Members.Contains(username) == false)
                {
                    packet = new S2C_Chat("시스템", "해당 방에 참여하고 있지 않습니다.");
                }
                else
                {
                    Room room = roomList[roomName];
                    StringBuilder text = new StringBuilder();
                    text.Append("방 정보를 확인합니다.\n\n");
                    text.Append($"= = = = = = [{roomName}] = = = = = =\n");
                    text.Append($"설명 : {room.Topic}\n");
                    text.Append('\n');
                    text.Append($"방장 : {room.Owner}\n");
                    text.Append($"멤버 : {room.Members.Count}명\n");
                    text.Append('\n');
                    text.Append($"공개설정 : {(room.IsPublic == true ? "공개" : "비공개")}\n");
                    text.Append("= = = = = = = = = = = = = = =\n\n");

                    packet = new S2C_Chat("시스템", text.ToString());
                }
            }

            string json = JsonConvert.SerializeObject(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void CreateCommand(Socket clientSock, string username, string roomName) // /방 <이름> 생성
        {
            S2C_Chat packet;
            lock (_lockObj)
            {
                // 1. <이름>의 방이 이미 존재하는지 확인
                if (roomList.ContainsKey(roomName) == true)
                {
                    packet = new S2C_Chat("시스템", "이미 존재하는 방입니다.");
                }
                else
                {
                    Room room = new Room(username, roomName);

                    roomList.Add(roomName, room);

                    packet = new S2C_Chat("시스템", $"{roomName} 방을 생성하였습니다.");
                }
            } // lock 키워드

            string json = JsonConvert.SerializeObject(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void MembersCommand(Socket clientSock, string username, string roomName) // /방 <이름> 명단
        {
            S2C_Chat packet;

            lock (_lockObj)
            {
                // 1. 방에 존재하는지, 참여하고 있는지 확인
                if (roomList.ContainsKey(roomName) == false || roomList[roomName].Members.Contains(username) == false)
                {
                    packet = new S2C_Chat("시스템", "해당 방에 참여하고 있지 않습니다.");
                }
                else
                {
                    Room room = roomList[roomName];

                    StringBuilder text = new StringBuilder();
                    text.Append($"{roomName} 방의 멤버 [{room.Members.Count}명] :\n");

                    // 방의 멤버 닉네임을 알파벳 순으로 정렬
                    List<string> sortedMembers = room.Members.OrderBy(u => u, StringComparer.OrdinalIgnoreCase) // 대소문자 무시하고 알파벳순 정렬
                                                          .ThenBy(u => u, StringComparer.Ordinal) // 스펠링이 같으면 대문자 우선 정렬
                                                          .ToList();

                    text.AppendJoin(", ", sortedMembers);

                    packet = new S2C_Chat("시스템", text.ToString());
                }
            }

            string json = JsonConvert.SerializeObject(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void JoinCommand(Socket clientSock, string username, string roomName) // /방 <이름> 참가
        {
            S2C_Chat packet;

            lock (_lockObj)
            {
                // 1. 방이 존재하는지 확인
                if (roomList.ContainsKey(roomName) == false)
                {
                    packet = new S2C_Chat("시스템", "방이 존재하지 않습니다.");
                }
                else
                {
                    Room room = roomList[roomName];
                    // 2. 이미 참여중인지 확인
                    if (room.Members.Contains(username) == true)
                    {
                        packet = new S2C_Chat("시스템", "이미 참여 중인 방입니다.");
                    }
                    // 3. 공개 방인지 확인
                    else if (room.IsPublic == false)
                    {
                        packet = new S2C_Chat("시스템", "공개 설정된 방이 아닙니다.");
                    }
                    else
                    {
                        bool isSuccess = room.JoinMember(username);

                        if (isSuccess == true) // 가입 처리 되었을 경우
                        {
                            packet = new S2C_Chat("시스템", $"{roomName} 방에 가입하였습니다.");

                            // TODO : 방 멤버 모두한테 입장 메세지 출력
                        }
                        else
                        {
                            packet = new S2C_Chat("시스템", "잘못된 접근입니다.");
                        }
                    }
                }
            } // lock 키워드

            string json = JsonConvert.SerializeObject(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void QuitCommand(Socket clientSock, string username, string roomName) // /방 <이름> 나가기
        {
            S2C_Chat packet;

            lock (_lockObj)
            {
                // 1. 방이 존재하는지, 해당 방에 참가되어있는지 확인
                if (roomList.ContainsKey(roomName) == false || roomList[roomName].Members.Contains(username) == false)
                {
                    packet = new S2C_Chat("시스템", "해당 방에 참여하고 있지 않습니다.");
                }
                else
                {
                    Room room = roomList[roomName];

                    bool isSuccess = room.QuitMember(username); // 나가기 처리

                    if (isSuccess == true) // 처리가 제대로 되었을 경우
                    {
                        if (room.Owner.Equals(username) == true) // 유저가 방장이고, 최후의 멤버였을 경우, 방 삭제
                        {
                            roomList.Remove(roomName);
                        }

                        packet = new S2C_Chat("시스템", $"{roomName} 방에서 퇴장하였습니다.");

                        // TODO : 방 멤버 모두한테 퇴장 메세지 출력
                    }
                    else // 처리가 제대로 되지 않았을 경우(해당 유저가 방장일 경우 / 멤버가 아닌 경우는 이미 걸렀으므로 제외)
                    {
                        packet = new S2C_Chat("시스템", "방장을 위임하거나 모든 멤버를 추방시킨 후 나갈 수 있습니다.");
                    }
                }
            } // lock 키워드

            string json = JsonConvert.SerializeObject(packet);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void InviteCommand(Socket clientSock, string username, string roomName, string data) // /방 <이름> 초대 <유저>
        {
            S2C_Chat packet = null!;
            string json;
            byte[] buffer;

            bool verifiedData = false;

            lock (_lockObj)
            {
                // 1. 방이 존재하는지, 해당 방에 참가되어있는지 확인
                if (roomList.ContainsKey(roomName) == false || roomList[roomName].Members.Contains(username) == false)
                {
                    packet = new S2C_Chat("시스템", "해당 방에 참여하고 있지 않습니다.");
                }
                else
                {
                    Room room = roomList[roomName];

                    // 2. 방의 방장인지 확인
                    if (room.Owner.Equals(username) == false)
                    {
                        packet = new S2C_Chat("시스템", "권한이 없습니다.");
                    }
                    else
                    {
                        // 3. 데이터에서 <유저>닉네임이 있는지 확인
                        if (data.Length <= 0 || data.Length > 12 || data.Contains(' ') == true || data.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) == false)
                        {
                            packet = new S2C_Chat("시스템", "잘못된 명령어입니다. (/방 <이름> 초대 <유저>)");
                        }
                        else
                        {
                            verifiedData = true;
                        }
                    }
                }
            } // lock 키워드

            if (verifiedData == true)
            {
                string target = data;
                // 4. 접속 중인 유저인지 확인
                Socket? targetSock = ChatServer.GetConnectUserSocket(target);
                if (targetSock == null)
                {
                    packet = new S2C_Chat("시스템", "해당 유저는 현재 접속 중이지 않습니다.");
                }
                else
                {
                    // 5. 최근 초대로 추가
                    lock (_lockObj)
                    {
                        recentInvite[target] = roomName;
                    }

                    // 6. 유저에게 초대메세지 패킷 전송
                    S2C_Chat targetPacket = new S2C_Chat("시스템", $"{roomName} 방에 초대 되었습니다. (/방 [수락/거절])");

                    json = JsonConvert.SerializeObject(targetPacket);
                    buffer = Encoding.UTF8.GetBytes(json);

                    targetSock.Send(buffer);

                    // 7. 요청자에게 패킷 전송
                    packet = new S2C_Chat("시스템", $"{target}님에게 초대요청을 전송하였습니다.");
                }
            }

            json = JsonConvert.SerializeObject(packet);
            buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void KickCommand(Socket clientSock, string username, string roomName, string data) // /방 <이름> 추방 <유저>
        {
            S2C_Chat packet;
            string json;
            byte[] buffer;

            bool iskick = false;

            lock (_lockObj)
            {
                // 1. 방이 존재하는지, 해당 방에 참가되어있는지 확인
                if (roomList.ContainsKey(roomName) == false || roomList[roomName].Members.Contains(username) == false)
                {
                    packet = new S2C_Chat("시스템", "해당 방에 참여하고 있지 않습니다.");
                }
                else
                {
                    Room room = roomList[roomName];

                    // 2. 방의 방장인지 확인
                    if (room.Owner.Equals(username) == false)
                    {
                        packet = new S2C_Chat("시스템", "권한이 없습니다.");
                    }
                    else
                    {
                        // 3. 데이터에서 <유저>닉네임이 있는지 확인
                        if (data.Length <= 0 || data.Length > 12 || data.Contains(' ') == true || data.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) == false)
                        {
                            packet = new S2C_Chat("시스템", "잘못된 명령어입니다. (/방 <이름> 추방 <유저>)");
                        }

                        string target = data;
                        // 4. 멤버인지 확인
                        if (room.Members.Contains(target) == false)
                        {
                            packet = new S2C_Chat("시스템", "해당 유저는 멤버가 아닙니다.");
                        }
                        else
                        {
                            // 5. 본인인지 확인
                            if (target == username)
                            {
                                packet = new S2C_Chat("시스템", "자기 자신은 추방할 수 없습니다.");
                            }
                            else
                            {
                                // 6. 추방 처리
                                room.QuitMember(target);

                                iskick = true;

                                // 7. 요청자에게 패킷 전송
                                packet = new S2C_Chat("시스템", $"{data}님의 추방하였습니다.");
                            }
                        }
                    }
                }
            } // lock 키워드

            // 8. 유저에게 초대메세지 패킷 전송
            if (iskick == true)
            {
                Socket? targetSock = ChatServer.GetConnectUserSocket(data);
                if (targetSock != null)
                {
                    S2C_Chat targetPacket = new S2C_Chat("시스템", $"{roomName} 방에서 추방 처리되었습니다.");

                    json = JsonConvert.SerializeObject(targetPacket);
                    buffer = Encoding.UTF8.GetBytes(json);

                    targetSock.Send(buffer);
                }

                // TODO : 방 멤버 모두에게 퇴장 메세지 출력
            }

            json = JsonConvert.SerializeObject(packet);
            buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void PrivacyCommand(Socket clientSock, string username, string roomName, string data) // /방 <이름> 공개설정 [공개/비공개]
        {
            S2C_Chat packet;
            string json;
            byte[] buffer;

            lock (_lockObj)
            {
                // 1. 방이 존재하는지, 해당 방에 참가되어있는지 확인
                if (roomList.ContainsKey(roomName) == false || roomList[roomName].Members.Contains(username) == false)
                {
                    packet = new S2C_Chat("시스템", "해당 방에 참여하고 있지 않습니다.");
                }
                else
                {
                    Room room = roomList[roomName];

                    // 2. 방의 방장인지 확인
                    if (room.Owner.Equals(username) == false)
                    {
                        packet = new S2C_Chat("시스템", "권한이 없습니다.");
                    }
                    else
                    {
                        // 3. 데이터 확인
                        bool? isPublic = null;
                        switch (data.ToLower())
                        {
                            case "공개":
                            case "public":
                                isPublic = true;
                                break;

                            case "비공개":
                            case "private":
                                isPublic = false;
                                break;
                        }

                        if (isPublic == null)
                        {
                            packet = new S2C_Chat("시스템", "잘못된 명령어입니다. (/방 <이름> 공개설정 [공개/비공개])");
                        }
                        else
                        {
                            // 5. 방 설정 적용
                            room.SetPrivacy(isPublic.Value);

                            // TODO : 멤버한테 수정된 공개설정 출력

                            // 7. 요청자에게 패킷 전송
                            packet = new S2C_Chat("시스템", $"{roomName} 방의 공개설정이 {(isPublic == true ? "공개" : "비공개")}로 설정되었습니다.");
                        }
                    }
                }
            } // lock 키워드

            json = JsonConvert.SerializeObject(packet);
            buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void DelegateCommand(Socket clientSock, string username, string roomName, string data) // /방 <이름> 방장위임 <유저>
        {
            S2C_Chat packet;
            string json;
            byte[] buffer;

            bool isDelegate = false;

            lock (_lockObj)
            {
                // 1. 방이 존재하는지, 해당 방에 참가되어있는지 확인
                if (roomList.ContainsKey(roomName) == false || roomList[roomName].Members.Contains(username) == false)
                {
                    packet = new S2C_Chat("시스템", "해당 방에 참여하고 있지 않습니다.");
                }
                else
                {
                    Room room = roomList[roomName];

                    // 2. 방의 방장인지 확인
                    if (room.Owner.Equals(username) == false)
                    {
                        packet = new S2C_Chat("시스템", "권한이 없습니다.");
                    }
                    else
                    {
                        // 3. 데이터에서 <유저>닉네임이 있는지 확인
                        if (data.Length <= 0 || data.Length > 12 || data.Contains(' ') == true || data.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) == false)
                        {
                            packet = new S2C_Chat("시스템", "잘못된 명령어입니다. (/방 <이름> 추방 <유저>)");
                        }

                        string target = data;
                        // 4. 멤버인지 확인
                        if (room.Members.Contains(target) == false)
                        {
                            packet = new S2C_Chat("시스템", "해당 유저는 멤버가 아닙니다.");
                        }
                        else
                        {
                            // 5. 본인인지 확인
                            if (target == username)
                            {
                                packet = new S2C_Chat("시스템", "자기 자신을 방장으로 위임할 수 없습니다.");
                            }
                            else
                            {
                                // 6. 위임처리
                                room.DelegateOwner(target);

                                isDelegate = true;

                                // 7. 요청자에게 패킷 전송
                                packet = new S2C_Chat("시스템", $"{data}님에게 방장을 위임하였습니다.");
                            }
                        }
                    }
                }
            } // lock 키워드

            // 8. 유저에게 위임 안내
            if (isDelegate == true)
            {
                Socket? targetSock = ChatServer.GetConnectUserSocket(data);
                if (targetSock != null)
                {
                    S2C_Chat targetPacket = new S2C_Chat("시스템", $"{roomName} 방의 방장으로 위임되었습니다.");

                    json = JsonConvert.SerializeObject(targetPacket);
                    buffer = Encoding.UTF8.GetBytes(json);

                    targetSock.Send(buffer);
                }

                // TODO : 방 멤버 모두에게 위임 메세지 출력
            }

            json = JsonConvert.SerializeObject(packet);
            buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void SetTopicCommand(Socket clientSock, string username, string roomName, string data) // /방 <이름> 설명 <내용>
        {
            S2C_Chat packet;
            string json;
            byte[] buffer;

            lock (_lockObj)
            {
                // 1. 방이 존재하는지, 해당 방에 참가되어있는지 확인
                if (roomList.ContainsKey(roomName) == false || roomList[roomName].Members.Contains(username) == false)
                {
                    packet = new S2C_Chat("시스템", "해당 방에 참여하고 있지 않습니다.");
                }
                else
                {
                    Room room = roomList[roomName];

                    // 2. 방의 방장인지 확인
                    if (room.Owner.Equals(username) == false)
                    {
                        packet = new S2C_Chat("시스템", "권한이 없습니다.");
                    }
                    else
                    {
                        // 3. 최근 설명 변경
                        room.SetTopic(data);

                        // 4. 요청자에게 패킷 전송
                        packet = new S2C_Chat("시스템", $"{roomName} 방 설명을 수정하였습니다.");

                        // TODO : 방 멤버 모두에게 설명 수정 메세지 출력
                    }
                }
            } // lock 키워드

            json = JsonConvert.SerializeObject(packet);
            buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }

        internal static void RenameCommand(Socket clientSock, string username, string roomName, string data) // /방 <이름> 이름변경 <새 이름>
        {
            S2C_Chat packet;
            string json;
            byte[] buffer;

            bool isPossible = false;

            lock (_lockObj)
            {
                // 1. 방이 존재하는지, 해당 방에 참가되어있는지 확인
                if (roomList.ContainsKey(roomName) == false || roomList[roomName].Members.Contains(username) == false)
                {
                    packet = new S2C_Chat("시스템", "해당 방에 참여하고 있지 않습니다.");
                }
                else
                {
                    Room room = roomList[roomName];

                    // 2. 방의 방장인지 확인
                    if (room.Owner.Equals(username) == false)
                    {
                        packet = new S2C_Chat("시스템", "권한이 없습니다.");
                    }
                    else
                    {
                        // 3. 데이터에서 <유저>닉네임이 있는지 확인
                        if (data.Length <= 0 || data.Contains(' ') == true)
                        {
                            packet = new S2C_Chat("시스템", "잘못된 명령어입니다. (/방 <이름> 이름변경 <새이름>)");
                        }
                        else
                        {
                            // 4. 이미 존재하는 방 이름인지 확인

                            if (roomList.ContainsKey(data) == true)
                            {
                                packet = new S2C_Chat("시스템", "이미 존재하는 방 이름으로는 변경할 수 없습니다.");
                            }
                            else
                            {
                                // 5. 이름 변경
                                room.Rename(data);
                                roomList.Remove(roomName);
                                roomList.Add(data, room);

                                isPossible = true;

                                // 7. 요청자에게 패킷 전송
                                packet = new S2C_Chat("시스템", $"{roomName} 방의 이름을 {data}로 변경했습니다.");

                                // TODO : 방 멤버 모두에게 이름변경 메세지 출력
                            }
                        }
                    }
                }
            } // lock 키워드

            if (isPossible == true)
            {
                ChatServer.RenameLinkRoomUsers(roomName, data);
            }

            json = JsonConvert.SerializeObject(packet);
            buffer = Encoding.UTF8.GetBytes(json);

            clientSock.Send(buffer);
        }
        
        internal static List<string> GetRoomMembers(string roomName)
        {
            lock (_lockObj)
            {
                if (roomList.ContainsKey(roomName) == false) return new List<string>();

                return roomList[roomName].Members.ToList();
            }
        }
    }
}
