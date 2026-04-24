namespace ChatServer
{
    internal static class ChatLogger
    {
        private static readonly object _lockObj = new object(); // lock 키워드용
        private static string fileName = "chatlog";

        public static void SaveLog(string message) // 로그를 저장하는 함수
        {
            lock (_lockObj)
            {
                try
                {
                    string logText = $"[{DateTime.Now.ToString("HH:mm:ss")}] {message}\n";
                    Console.Write(logText);

                    File.AppendAllText($"{fileName}-{DateTime.Now.ToString("yyyy-MM-dd")}.txt", logText);

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[로그 저장 실패] {ex.Message}");
                }
            } // lock 키워드
        }
    }
}
