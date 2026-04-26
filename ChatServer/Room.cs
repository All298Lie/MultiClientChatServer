namespace ChatServer
{
    internal class Room // 방(Room) 클래스
    {
        public string Name { get; private set; } // 방 이름
        public string Topic { get; private set; } // 방 설명

        public string Owner { get; private set; } // 방장

        private List<string> members; // 방 멤버
        public IReadOnlyList<string> Members { get { return members; } } // 외부에서는 멤버 수정 불가능

        public bool IsPublic { get; private set; } // 방 공개여부

        public Room(string owner, string name) // 생성자. 방 명령어 권한 같은 경우 외부에서 조정 필요
        {
            this.Name = name;
            this.Topic = "";

            this.Owner = owner;

            this.members = new List<string>();
            this.members.Add(owner);

            this.IsPublic = false;
        }

        public void Rename(string name) // 방 이름변경 (이름 중복 검사는 외부에서 탐색)
        {
            this.Name = name;
        }

        public void SetTopic(string topic) // 방 설명설정
        {
            this.Topic = topic;
        }

        public bool DelegateOwner(string newOwner) // 방장 위임
        {
            if (this.members.Contains(newOwner) == false) return false;
            if (this.Owner == newOwner) return false;

            // 새 방장이 해당 방에 들어와있을 때만 위임 가능
            this.Owner = newOwner;

            return true;
        }

        public void SetPrivacy(bool isPublic) // 공개여부 설정
        {
            this.IsPublic = isPublic;
        }

        public bool JoinMember(string name) // 멤버 추가
        {
            if (this.members.Contains(name) == true) return false;

            this.members.Add(name);
            return true;
        }

        public bool QuitMember(string name) // 멤버 제거
        {
            if (this.members.Contains(name) == false) return false; // 멤버가 아닐 경우

            if (this.Owner == name && this.Members.Count > 1) return false; // 방장은 방장위임 후 탈퇴 가능

            this.members.Remove(name);
            return true;
        }
    }
}
