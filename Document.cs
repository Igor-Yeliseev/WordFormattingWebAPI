namespace DocumentFormattingWebAPI
{
    public enum DocumentType
    {
        PDF,
        DOCX,
        TXT
    }

    public class Document
    {
        public int Id { get; set; }
        public string? Text { get; set; }
        public DateTime LoadDate { get; set; }
        public DocumentType Type { get; set; }
        public string? FileName { get; set; }
        public byte[]? FileData { get; set; }
    }
}
