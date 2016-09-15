using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JovemNerd
{
    class Episode
    {
        public string id { get; set; }
        public string url { get; set; }
        public string pub_date { get; set; }
        public Double duration { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string episode { get; set; }
        public string subject { get; set; }
        public string image { get; set; }
        public string audio_high { get; set; }
        public string audio_medium { get; set; }
        public string audio_low { get; set; }
        public List<Insertions> insertions { get; set; }    
        public string guests { get; set; }
        public string jump_to_time { get; set; }
        public string product { get; set; }
        public string slug { get; set; }
    }

    class Insertions
    {
        public string id { get; set; }
        public string image { get; set; }
        public string link { get; set; }
        public string start_time { get; set; }
        public string end_time { get; set; }
    }
}
