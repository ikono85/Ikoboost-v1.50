namespace IkoboostWpf.Models;

public record SpeedTestRecord(
    string Time,
    double PingMs,
    double DownloadMbps,
    double UploadMbps,
    double JitterMs
);
