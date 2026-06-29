namespace Xipha.Crawl.NationalFormulary.Models
{
    public class DrugBasic
    {
        public string PersianName { get; set; } = "";
        public string EnglishName { get; set; } = "";
        public string BrandOwner { get; set; } = "";
        public string LicenseHolder { get; set; } = "";
        public long Price { get; set; }
        public string Packaging { get; set; } = "";
        public string ProductCode { get; set; } = "";   // product code
        public string GenericCode { get; set; } = "";   // generic code
        public string DetailUrl { get; set; } = "";
        public string SearchTermUsed { get; set; } = "";
        public bool IsEmergencyLicense { get; set; }

        /// <summary>Numeric ID extracted from the trailing segment of DetailUrl</summary>
        public int WebId
        {
            get
            {
                var seg = DetailUrl.TrimEnd('/').Split('/');
                return int.TryParse(seg[^1], out int id) ? id : 0;
            }
        }
    }
}