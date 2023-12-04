using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddressReAssignment.Process
{
    public class OnigbanjoAddressVerificationRequest
    {//24-08-2023
        public string clientJobrefID { get; set; }
        public string ref_no { get; set; }//no need to pass, will be set by vendor
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string state { get; set; }
        public string city { get; set; }
        public string phone_number { get; set; }
        public string email { get; set; }
        public string address { get; set; }
        public string client { get; set; }//get from config
        public string published { get; set; } = "1";//get from config when set =0, it will be kept in draft , and when set =1, it will be available for
        //verification, Set to 1

    }

    public class iData
    {
        public iNote note { get; set; }
    }

    public class iNote
    {
        public string clientJobrefID { get; set; }
        public string ref_no { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string state { get; set; }
        public string city { get; set; }
        public string phone_number { get; set; }
        public string email { get; set; }
        public string address { get; set; }
        public string client { get; set; }
        public string published { get; set; }
    }



    public class MessageHolder
    {
        public List<string> non_field_errors { get; set; }
    }

    public class OnigbanjoFailureResponseb4
    {
        public string status { get; set; }
        public MessageHolder message { get; set; }
    }
    /// <summary>
    /// 
    /// </summary>

    public class Data
    {
        public Jobs jobs { get; set; }
    }




    public class Message
    {
        public List<string> non_field_errors { get; set; }
    }

    public class OnigbanjoFailureResponse
    {
        public string status { get; set; }
        public Message message { get; set; }
    }



    public class Jobs
    {
        public string clientJobrefID { get; set; }
        public string ref_no { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string state { get; set; }
        public string city { get; set; }
        public string phone_number { get; set; }
        public string email { get; set; }
        public string address { get; set; }
        public string client { get; set; }
        public string published { get; set; }
    }

    public class OnigbanjoResponse
    {
        public string status { get; set; }
        public Data data { get; set; }
    }


}
