namespace LMS.Models
{
    public class Certificate
    {
        public int Id { get; set; }
        
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        
        public int CourseId { get; set; }
        public Course? Course { get; set; }
        
        public string CertificateNumber { get; set; } = string.Empty;
        
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? DownloadedAt { get; set; }
        
        public string StudentName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        
        public static string GenerateCertificateNumber()
        {
            return $"CERT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        }
    }
}
