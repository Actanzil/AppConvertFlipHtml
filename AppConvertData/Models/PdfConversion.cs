namespace AppConvertData.Models
{
    public class PdfConversion
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string OutputFolder { get; set; }
        public int PageCount { get; set; }
        public DateTime ConversionDate { get; set; }
    }

}
