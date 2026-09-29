using System.Text.Json.Serialization;

namespace LoggingActivity.Web.Models;

// Bản ghi trả về từ API LOS get_log_call_api (giữ nguyên tên trường của API, kể cả "loanAmout").
public sealed class CustomerApiCallLog
{
    [JsonPropertyName("loanBriefId")]
    public long LoanBriefId { get; set; }

    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }

    [JsonPropertyName("status")]
    public int? Status { get; set; }

    [JsonPropertyName("statusName")]
    public string? StatusName { get; set; }

    [JsonPropertyName("loanAmout")]
    public decimal? LoanAmount { get; set; }

    [JsonPropertyName("loanTime")]
    public int? LoanTime { get; set; }

    [JsonPropertyName("productId")]
    public int? ProductId { get; set; }

    [JsonPropertyName("productName")]
    public string? ProductName { get; set; }

    [JsonPropertyName("rateTypeId")]
    public int? RateTypeId { get; set; }

    [JsonPropertyName("rateTypeName")]
    public string? RateTypeName { get; set; }

    [JsonPropertyName("disbursementPartner")]
    public string? DisbursementPartner { get; set; }

    [JsonPropertyName("createdTime")]
    public DateTime? CreatedTime { get; set; }

    [JsonPropertyName("disbursementAt")]
    public DateTime? DisbursementAt { get; set; }

    // Thời điểm khách hàng gọi API, theo giờ Việt Nam.
    [JsonPropertyName("createdTimeLog")]
    public DateTime CreatedTimeLog { get; set; }

    [JsonPropertyName("actionCall")]
    public int ActionCall { get; set; }

    [JsonPropertyName("actionCallName")]
    public string? ActionCallName { get; set; }
}
