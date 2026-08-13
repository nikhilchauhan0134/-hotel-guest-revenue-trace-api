namespace HotelGraphApi.Configuration;

public class CognoDbSettings
{
    public const string SectionName = "CognoDb";

    public string Uri { get; set; } = string.Empty;
    public string Username { get; set; } = "cognodb";
    public string Password { get; set; } = string.Empty;
}
