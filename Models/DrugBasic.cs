using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xipha.Crawl.Models
{
    public class DrugBasic
    {
        public string PersianName { get; set; } = "";
        public string EnglishName { get; set; } = "";
        public string BrandOwner { get; set; } = "";
        public string LicenseHolder { get; set; } = "";
        public long Price { get; set; }
        public string Packaging { get; set; } = "";
        public string ProductCode { get; set; } = "";   // کد فرآورده
        public string GenericCode { get; set; } = "";   // کد ژنریک
        public string DetailUrl { get; set; } = "";
        public string SearchTermUsed { get; set; } = "";
        public bool IsEmergencyLicense { get; set; }

        /// <summary>شناسه عددی از انتهای DetailUrl</summary>
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
