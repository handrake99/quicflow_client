using System;

namespace QuicFlowClient.Models;

public record ChatData
{
    // JsonPropertyName을 쓰면 C# 속성명(PascalCase)과 JSON 키(camelCase)를 다르게 매핑 가능
    // 여기서는 헷갈리지 않게 그냥 둡니다.
    public string Type { get; set; } = "";
    public uint MessageId { get; set; } = 0;
    public string UserID { get; set; } = "";
    public string Message { get; set; } = "";
    public long Timestamp { get; set; }

    public ChatData(string type, uint messageId, string userID, string message)
    {
        Type = type;
        MessageId = messageId;
        UserID = userID;   
        Message = message;
        Timestamp = DateTime.Now.Ticks;
    }
}