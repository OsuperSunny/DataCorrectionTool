using CAFTDomain.Aggregate;
using DomainManager.Helper;
using DomainManager.Worker;
using DomainManager;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataRepository.Interface;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
using DataRepository.Implementation;
using System.Net;
using CAFTDomain.ValueObject;
using Microsoft.Extensions.Configuration;
using DocumentAssignment.Process;
using DocumentAssignment;
using System.Reflection;
using SharedKernel;
using DataMigrationTool.Process;
using System.Data;

using System.Collections;
using System.Net.Http;
using SharedKernel.Models;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using GemBox.Spreadsheet.Drawing;

namespace AddressReAssignment.Process
{
    public class ProcessCustomerAddress : IProcessDimecCustomer
    {
        ILogger<ProcessCustomerAddress> _log;
        private readonly IOptions<KeyValues> _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IDataRepository<ResidentialAddress> _residentialAddress;
        private readonly IDataRepository<Customer> _customer;
        private readonly IDataRepository<AddressVerificationActivity> _AddressVerification;
        private readonly IDataRepository<Remarks> _rem;
        private readonly IDataRepository<AddressVerificationVendor> _addVendor;
        private readonly IDataRepository<PublicHoliday> _publicHol;
        public ProcessCustomerAddress(IOptions<KeyValues> config, ILogger<ProcessCustomerAddress> log, IHttpClientFactory httpClientFactory, IDataRepository<AddressVerificationVendor> addVendor, IDataRepository<AddressVerificationActivity> AddressVerification, IDataRepository<Customer> customer, IDataRepository<Remarks> rem, IDataRepository<ResidentialAddress> residentialAddress, IDataRepository<PublicHoliday> publicHol)
        {
            _config = config;
            _log = log;
            _httpClientFactory = httpClientFactory;
            _customer = customer;
            _addVendor = addVendor;
            _AddressVerification = AddressVerification;
            _rem = rem;
            _residentialAddress = residentialAddress;
            _publicHol = publicHol;

        }

        public static bool AreDifferent<T>(T a, T b)
        {
            var properties = typeof(T).GetProperties();
            foreach (var property in properties)
            {
                var valueA = property.GetValue(a);
                var valueB = property.GetValue(b);
                if (!Equals(valueA, valueB))
                {
                    return true;
                }
            }
            return false;
        }

        public static ResidentialAddress FromJson(string json)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<ResidentialAddress>(json);
        }

        public void ReviewAndProcessCustomerAddress(IConfiguration config)
        {
            List<string> CiFList = new List<string>();
            //for personal Testing
            #region AddressTesting
            //Testing the update for addressverification here
            try
            {
                string json = @"
                {
                  ""Id"": ""047D0CC7-BD84-421B-89B2-3BBAC4667259"",
                  ""CustomerId"": ""DFCCB572-D6FB-452D-89FA-0000019764F4"",
                  ""CIF"": ""R005419247"",
                  ""BuildingNumber"": ""9"",
                  ""Apartment"": ""flat"",
                  ""Street"": ""ligali"",
                  ""City"": ""Bende"",
                  ""Town"": ""Bende"",
                  ""State"": ""Abia State"",
                  ""Lga"": ""Arochukwu"",
                  ""Lcda"": """",
                  ""DateCreated"": ""2023-03-07T12:25:32.7004285+01:00"",
                  ""Landmark"": ""Ligali bus stop"",
                  ""AdditionalInformation"": ""ddfd"",
                  ""Country"": ""Nigeria"",
                  ""FullAddress"": ""flat, 9, ligali, Ligali bus stop, Arochukwu, Bende, Bende, Nigeria - ddfd"",
                  ""Status"": 1,
                  ""ChannelID"": ""d7d37454-dccf-4bf7-9705-d8209bfa9d48"",
                  ""ChannelName"": ""ALAT"",
                  ""Customer"": null,
                  ""AddressVerificationActivities"": null
                }";

                ResidentialAddress myObject = FromJson(json);

                json = @"
                {
                  ""Id"": ""047D0CC7-BD84-421B-89B2-3BBAC4667259"",
                  ""CustomerId"": ""DFCCB572-D6FB-452D-89FA-0000019764F4"",
                  ""CIF"": ""R005419247"",
                  ""BuildingNumber"": ""9"",
                  ""Apartment"": ""flat"",
                  ""Street"": ""ligali"",
                  ""City"": ""Bende"",
                  ""Town"": ""Bende"",
                  ""State"": ""Abia State"",
                  ""Lga"": ""Arochukwu"",
                  ""Lcda"": """",
                  ""DateCreated"": ""2023-03-07T12:25:32.7004285+01:00"",
                  ""Landmark"": ""Ligali bus stop"",
                  ""AdditionalInformation"": ""ddfd"",
                  ""Country"": ""Nigeria"",
                  ""FullAddress"": ""Block, 9, ligali, Ligali bus stop, Arochukwu, Bende, Bende, Nigeria - ddfd"",
                  ""Status"": 1,
                  ""ChannelID"": ""d7d37454-dccf-4bf7-9705-d8209bfa9d48"",
                  ""ChannelName"": ""ALAT"",
                  ""Customer"": null,
                  ""AddressVerificationActivities"": null
                }";

                ResidentialAddress myObject2 = FromJson(json);

                var classA = myObject;
                var classB = myObject2;
                if (AreDifferent(classA, classB))
                {
                    // Console.WriteLine("The classes are different.");
                }
                else
                {
                    // Console.WriteLine("The classes are the same.");
                }
                //working well


                var updateRes = _residentialAddress.Update(myObject);
            }
            catch (Exception ex)
            {

            }

            //
            #endregion AddressTesting
            var numOfDays = Convert.ToInt16(config.GetRequiredSection("DaysBack").Value);
            bool isDemo = Convert.ToBoolean(config.GetRequiredSection("IsDemo").Value);
            int RecordsToTake = Convert.ToInt16(config.GetRequiredSection("RecordsToTake").Value);
            var CIFList = config.GetRequiredSection("CIFList").Value.Split(",").ToList();
            int ProcessStatus = Convert.ToInt16(config.GetRequiredSection("ProcessStatus").Value);
            string remarkForSearch = config.GetRequiredSection("remarkForSearch").Value;
            string VendorClientId = config.GetRequiredSection("VendorClientId").Value;
            string Published = config.GetRequiredSection("Published").Value;


            string fileName = "Address Issues.xlsx";
            try
            {
                string filePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), fileName);

                string p = AppDomain.CurrentDomain.BaseDirectory;

                fileName = ReadDataFromFile.GetFilesByExtension(p, ".xlsx", SearchOption.AllDirectories).First().ToString();
                Console.WriteLine($"AddressReAssignment =====PROCESSING OPERATION Started!=============");
                //Get a list of cifs to check against
                var DataList = ReadDataFromFile.ConvertExcel(fileName);
                Console.WriteLine($"AddressReAssignment Found {DataList.Rows.Count} CIF Row(s) in the Excel File for checkup and migration! ");
                try
                {
                    CiFList = (from row in DataList.AsEnumerable().Where(x => x.Field<string>("CIF") != null && x.Field<string>("CIF").Length >= 10) select row["CIF"].ToString()).ToList();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"AddressReAssignment Error traversing the list in Mem! MSG:{ex.Message}<<>>>>{ex.StackTrace} ");
                    CiFList = (from row in DataList.AsEnumerable().Where(x => x.Field<string>("CIF_ID") != null && x.Field<string>("CIF_ID").Length >= 10) select row["CIF"].ToString()).ToList();
                }

                List<string> mergedList = CiFList.Concat(CIFList).ToList();

                Console.WriteLine($"AddressReAssignment Found {DataList.Rows.Count} CIF Row(s) in the Excel File for Address Reassignment Op| Also, the CIF param from Config:{CIFList.Count}, Total Found:{mergedList.Count}");
                //call this method to process Address Reassignment
                CreateAddressVerificationActivity(CiFList, ProcessStatus, remarkForSearch);

                Console.WriteLine($"AddressReAssignment Found {DataList.Rows.Count} CIF Row(s) in the Excel File for Address Reassignment Op|  Also, the CIF param from Config:{CIFList.Count}, Total Found:{mergedList.Count}|Now COmpleted! ");
            } catch (Exception ex) { Console.WriteLine($" ERROR While Accessing File: MSG|STrace - {ex.Message}|{ex.StackTrace}"); }
        }

        public ResponseHandler CreateAddressVerificationActivity(List<string> CustomerCIFs, int status, string remarkKeyword = "", string VendorClientId ="", string Published="1")
        {
            ResidentialAddress resAddress = new ResidentialAddress();
            ResponseHandler<Customer> c = new ResponseHandler<Customer>();
            var resHandler = new ResponseHandler();
            string vendorName = string.Empty;
            bool SkipRemark = true;
            bool reAssign = false;
            var actRes = new ResponseHandler<AddressVerificationActivity>();
            bool isAddressSent = false;
            bool getPushed = false;
            Console.WriteLine($"AddressReAssignment Status:{status}|RemarkKeyword:{remarkKeyword} <==> About to Process:{CustomerCIFs.Count} Found...");
            //get customer//get adddress//get address activity
            foreach (var customer in CustomerCIFs)
            {
                try
                {
                    if (string.IsNullOrEmpty(customer))
                    {
                        isAddressSent = false;
                        resHandler.StatusCode = (int)HttpStatusCode.BadRequest;
                        resHandler.Message = "Customer data is required!";
                        Console.WriteLine($"AddressReAssignment Cutsomer to Process- CIF: {customer} No Customer data Found...");
                        // return resHandler;
                        continue;
                    }
                    c = _customer.GetSingle(x => x.CIF == customer && x.IsResidentialAddressApproved == false);
                    if (c.SingleResult == null)
                    {
                        isAddressSent = false;
                        resHandler.StatusCode = (int)HttpStatusCode.BadRequest;
                        resHandler.Message = "Customer data is required!";
                        Console.WriteLine($"AddressReAssignment Cutsomer to Process- CIF: {customer} No Customer Found...");
                        //return resHandler;
                        continue;
                    }

                    string vendorId = "";
                    Console.WriteLine($"AddressReAssignment Cutsomer to Process- CIF: {c.SingleResult.CIF}|Address:{c.SingleResult.Address} Found...");
                    resAddress = _residentialAddress.GetRecentRecord(x => x.CustomerId == c.SingleResult.Id, d => d.DateCreated).Result;
                    if (resAddress == null)
                    {
                        isAddressSent = false;
                        resHandler.StatusCode = (int)HttpStatusCode.BadRequest;
                        resHandler.Message = "Customer Address details not available!";
                        Console.WriteLine($"AddressReAssignment Cutsomer to Process- CIF: {customer} No Customer Address details Found...");
                        //return resHandler;
                        continue;
                    }
                    var addreAct = _AddressVerification.GetRecentRecord(x => (x.CustomerId == c.SingleResult.Id || x.ResidentialAddressId == resAddress.Id) && x.VerificationStatus < 2, d => d.DateCreated).Result;
                    if (addreAct == null)
                    {
                        isAddressSent = false;
                        resHandler.StatusCode = (int)HttpStatusCode.BadRequest;
                        resHandler.Message = "No Address Verification Activity Found!";
                        Console.WriteLine($"AddressReAssignment Cutsomer to Process- CIF: {customer} No Address Verification Activity Found...");
                        //"ProcessStatus": 1,//only when status is 4 that it will reassign afresh. Change status to 4 only when you are sure it has never been in existence. Put 5 to ignore status check.
                        if (status == 4||status==5)
                        {//we are creating new activity afresh for this category
                            //format the Residential Address Activity object
                            var addVerAct = AddressVerificationActivity.FormatAddressVerificationActivityRecord(resAddress, vendorId);
                            Console.WriteLine($"AddressReAssignment Cutsomer to Process- CIF: {customer}|Status:{status} No Address Verification Activity Found & We Are creating it new here on the status...");
                            var addreActIn = InsertNewActivityRecord(addVerAct);
                            if (addreActIn.StatusCode == (int)HttpStatusCode.OK)
                            {
                                Console.WriteLine($"AddressReAssignment Cutsomer to Process- CIF: {customer}|Status:{status} No Address Verification Activity Found & New Activity Created Successfully on the status...");
                                addreAct = addreActIn.SingleResult;
                            }
                            else
                            {
                                Console.WriteLine($"AddressReAssignment Cutsomer to Process- CIF: {customer}|Status:{status} No Address Verification Activity Found & New Activity failed on the status. Reason:{addreActIn.StatusCode}|{addreActIn.Message}...");
                                //return resHandler;

                                continue;
                            }
                        }
                        else
                        {//any other status is expected to be already in the activity table
                            //return resHandler;
                            continue;
                        }


                    }
                    if (addreAct != null)
                    {
                        actRes = new ResponseHandler<AddressVerificationActivity>
                        {
                            StatusCode = (int)HttpStatusCode.OK,
                            Message = "Success",
                            SingleResult = addreAct
                        };
                        vendorId = addreAct.VendorId;

                        //Let us check other status here excluding 4
                        //fetch from remarks table here
                        var RemarksContents = _rem.GetSingle(x => (c.SingleResult.Id != null && x.CustomerId == c.SingleResult.Id) || (addreAct.ActivityId != null && x.CustomerId == addreAct.ActivityId));
                        if (RemarksContents != null)
                        {
                            var RemarksContent = RemarksContents.SingleResult;
                            if (RemarksContent == null && status == 3)
                            {//allow the process to proceed
                                SkipRemark = false;
                                reAssign = true;
                                Console.WriteLine($"AddressReAssignment Cutsomer to Process- CIF: {customer}|Status:{status}. No Remark for this - on the status. ...");
                            }
                            else if (RemarksContent != null && status == 1)
                            {//check the response code to see if not 00, then proceed to do =>conditional reassignment
                                var Content = RemarksContent.CurrentStatus;
                                Content= ExtractStatusCode(Content);
                                if(Content != "00")
                                {
                                    Console.WriteLine($"AddressReAssignment (conditional) Cutsomer to Process- CIF: {customer}|Status:{status}. Remark for this {RemarksContent.CurrentStatus}|{Content}- on the status. ...");
                                    reAssign = true;
                                }

                            }
                            else if (RemarksContent != null && status == 2)
                            {//check the response code to see if is 00, then proceed to do =>intentional reassignment
                                var Content = RemarksContent.CurrentStatus;
                                Content = ExtractStatusCode(Content);
                                if (Content == "00")
                                {
                                    Console.WriteLine($"AddressReAssignment (intentional) Cutsomer to Process- CIF: {customer}|Status:{status}. Remark for this {RemarksContent.CurrentStatus}|{Content}- on the status. ...");
                                    reAssign = true;
                                }
                            }
                            else if (RemarksContent != null && status == 5 && !string.IsNullOrEmpty(remarkKeyword) && RemarksContent.CurrentStatus.ToLower().Contains(remarkKeyword.ToLower()))
                            {//Check that the response contains the filter=>Supply the keywords to use
                                Console.WriteLine($"AddressReAssignment (filtering) Cutsomer to Process- CIF: {customer}|Status:{status}. Remark for this {RemarksContent.CurrentStatus}|{remarkKeyword}- on the status. ...");
                                reAssign = true;
                            }
                        }
                        else if (RemarksContents == null && (status == 3||status==5))
                        {//still proceed
                            SkipRemark = false;
                            Console.WriteLine($"AddressReAssignment (Proceed) Cutsomer to Process- CIF: {customer}|Status:{status}. Remark for this - on the status. ...");
                            reAssign = true;
                        }


                    }

                    else
                        actRes = new ResponseHandler<AddressVerificationActivity>
                        {
                            StatusCode = (int)HttpStatusCode.NotFound,
                            Message = "Failed",
                            SingleResult = null
                        };

                    if (string.IsNullOrEmpty(vendorId))
                        vendorId = GetNextVendorForAddressVerification();

                    if (vendorId == "") vendorId = "6ce5c941-63c6-4da0-9639-dc7554d0a024";

                    if (!string.IsNullOrEmpty(vendorId))
                    {
                        //get vendor details. find out if he has an API
                        var vendDetails = GetVendor(new Guid(vendorId));
                        try
                        {
                            vendorName = vendDetails.SingleResult.CompanyName;
                        }
                        catch(Exception e)
                        {
                            Console.WriteLine($"AddressReAssignment: ERROR VendorDetails:{resAddress.CIF}|VendorId:{vendorId} | MSG :{e.Message}|STrace- {e.StackTrace} |INNEREX- {e.InnerException}");
                            resHandler.StatusCode = 400;
                            resHandler.Message = "Invalid Vendor details!";
                            return resHandler;
                        }

                        
                        if (vendDetails.SingleResult != null && c.SingleResult != null && reAssign)
                        {

                            if (vendDetails.SingleResult.HasAPI)
                            {
                                switch (vendDetails.SingleResult.AppIntegrationName.ToLower())
                                {
                                    case "youverify":
                                        var req = new YouVerifyCandidateRequestModel
                                        {
                                            email = c.SingleResult.Email,
                                            dob = c.SingleResult.DOB.ToString(),
                                            country = "Nigeria",
                                            first_name = c.SingleResult.FirstName ?? c.SingleResult.LastName,
                                            last_name = c.SingleResult.LastName ?? c.SingleResult.FirstName
                                        };

                                        resHandler = PushCustomerToYouVerify(req, resAddress, c.SingleResult, vendDetails.SingleResult, actRes.SingleResult,SkipRemark);
                                        getPushed = true;
                                        break;

                                    case "verifyme":
                                        resHandler = PushCustomerToVerifyMe(resAddress, c.SingleResult, vendDetails.SingleResult, actRes.SingleResult, SkipRemark);
                                        getPushed = true;
                                        break;

                                    case "zainab":
                                        resHandler = PushCustomerToSalisu(resAddress, c.SingleResult, vendDetails.SingleResult, actRes.SingleResult, SkipRemark);
                                        getPushed = true;
                                        break;

                                    case "onigbanjo":
                                        Console.WriteLine($"Sending to onigbanjo");
                                        PushCustomerToOnigbanjo(resAddress, c.SingleResult, vendDetails.SingleResult, actRes.SingleResult, SkipRemark, VendorClientId, Published);
                                        break;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"AddressReAssignment: AddressAssignedToVendor|{vendorName} _CreateActivity *:CIF:{resAddress.CIF}=>>>About sending mail to Vendor whenNoAPI...");
                                //contact vendor telling him he has been given an address to verify
                                var mail = new MailModel
                                {
                                    Body = $"Dear {vendDetails.SingleResult.CompanyName}, you have been assigned a customer from Wema Bank to perform address verification on. Kindly respond promptly. Kind Regards",
                                    Recipient = vendDetails.SingleResult.PrimaryContactEmail,
                                    Sender = "no-reply@alat.ng",
                                    Subject = "Address Verification Request (Wema Bank)"
                                };

                                var res = SendEmail(mail);
                                PushCustomerToOnigbanjo(resAddress, c.SingleResult, vendDetails.SingleResult, actRes.SingleResult, SkipRemark, VendorClientId, Published);
                                getPushed = true;
                                isAddressSent = true;
                                resHandler.StatusCode = (int)HttpStatusCode.OK;
                                resHandler.Message = $"Address successfully assigned to {vendorName}";
                                //resHandler.Message = "success";
                                Console.WriteLine($"AddressReAssignment: AddressAssignedToVendor|{vendorName} bymail:CIF:{resAddress.CIF}=>  Request: {mail.ToJson()}------response: {res}");
                            }
                        }
                        else
                        {
                            try
                            {
                                if (vendDetails.SingleResult == null)
                                {
                                    Console.WriteLine($"AddressReAssignment: No Vendor Details found:{resAddress.CIF}  Request: {resAddress.ToJson()} and Vendor details are null");
                                    resHandler.StatusCode = (int)HttpStatusCode.BadRequest;
                                    resHandler.Message = "Vendor details not found. Address saved as low risk";
                                }
                                else
                                {
                                    Console.WriteLine($"AddressReAssignment: No Customer Details:{resAddress.CIF}  Request: {resAddress.ToJson()} and Vendor details are real-{vendDetails.SingleResult.CompanyName}");
                                    resHandler.StatusCode = (int)HttpStatusCode.BadRequest;
                                    resHandler.Message = "Customer details not found. Address saved as low risk";
                                }
                            }
                            catch { }

                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"AddressReAssignment: ERROR AddressAssignedToVendorFailed:{resAddress.CIF}  Request: {resAddress.ToJson()} | MSG :{e.Message}|STrace- {e.StackTrace} |INNEREX- {e.InnerException}");
                    resHandler.Error = e;
                    resHandler.StatusCode = 500;
                    resHandler.Message = getPushed == false ? "An error occurred before pushing address to vendor!" : $"An error occurred but address was actually pushed to vendor {vendorName}!";
                    //throw;
                }
            }

            resHandler.Message += $"|{vendorName}";
            return resHandler;

        }

        public string ExtractStatusCode(string input)
        {
            //string input = "Response - {\"StatusCode\":\"99\",\"StatusMessage\":\"Service Failure! Please retry.\"}";

            // Define a regular expression pattern to match the status code
            string pattern = @"\""StatusCode\""\s*:\s*\""(\d+)\""";

            // Use Regex.Match to find the status code
            Match match = Regex.Match(input, pattern);

            if (match.Success)
            {
                string statusCode = match.Groups[1].Value;
               Console.WriteLine("Status Code: " + statusCode);
                input = statusCode;
            }
            else
            {
                Console.WriteLine("Status Code not found.");
                input = "";
            }

            return input;
       }

        public ResponseHandler<AddressVerificationActivity> InsertNewActivityRecord(AddressVerificationActivity c)
        {
            ResponseHandler<AddressVerificationActivity> response = null;
            
            //if a customer would be allowed to edit his address before the previously submitted address is back from verification then do this
            try
            {
                //check if a customer has pending address verification activity
                //var rrr = _activity.GetAll(p => p.CustomerId == c.CustomerId && p.VerificationStatus == (int)ResidentialAddressStatus.AwaitingVerification);
                var rrr = _AddressVerification.Exists(p => p.CustomerId == c.CustomerId && p.VerificationStatus == (int)ResidentialAddressStatus.AwaitingVerification);

                if (!rrr)
                {
                    var res = _AddressVerification.Insert(c);

                    if (res.StatusCode == (int)HttpStatusCode.OK)
                    {
                        response = new ResponseHandler<AddressVerificationActivity>
                        {
                            Error = res.Error,
                            Message = res.Message,
                            StatusCode = res.StatusCode,
                            SingleResult = res.SingleResult
                        };
                    }
                    else
                    {
                        response = new ResponseHandler<AddressVerificationActivity>
                        {
                            Error = res.Error,
                            Message = res.Message,
                            StatusCode = res.StatusCode
                        };
                    }
                }
                else
                {
                    Console.WriteLine($"AddressReAssignment: Customer has pending address verification activity request - {c.CustomerId}", nameof(InsertNewActivityRecord));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"AddressReAssignment: ERROR:{c.CustomerId}  Request: {c.ActivityId} | MSG :{e.Message}|STrace- {e.StackTrace} |INNEREX- {e.InnerException}");
            }

            return response;
        }

        private ResponseHandler PushCustomerToVerifyMe(ResidentialAddress res, Customer cus, AddressVerificationVendor vend, AddressVerificationActivity sv, bool SkipRemark = false)
        {
            var responseHandler = new ResponseHandler();
            bool isAddressSent = false;
            VerifyMeAddressVerificationResponse _resp = null;
            HttpClient _cli = new HttpClient();
            try
            {
                //then initiate address verification request from them
                var addReq = new VerifyMeAddressVerificationRequest
                {
                    applicant = new Applicant
                    {
                        firstname = cus.FirstName,
                        idNumber = FormatPhoneNumber(cus.MobileNumber), // for live use mobile number
                        idType = "kyc",
                        lastname = cus.LastName ?? cus.FirstName,
                        phone = cus.MobileNumber
                    },
                    city = res.City,
                    landmark = res.Landmark,
                    lga = res.Lga,
                    street = res.FullAddress,
                    state = res.State.ToLower().Contains("state") ? res.State.Remove(res.State.IndexOf(' ')) : res.State,
                    reference = sv.ActivityId.ToString()
                };


                _cli.DefaultRequestHeaders.Add("Authorization", "Bearer " + vend.AppKey);

                string contt = JsonConvert.SerializeObject(addReq);

                HttpResponseMessage resu = _cli.PostAsync($"{vend.BaseUrl}v1/verifications/addresses", new StringContent(contt, Encoding.UTF8, "application/json")).Result;
                var r = Encoding.UTF8.GetString(resu.Content.ReadAsByteArrayAsync().Result);
                _resp = JsonConvert.DeserializeObject<VerifyMeAddressVerificationResponse>(r);

                Console.WriteLine($"AddressReAssignment:Data to Verify Me - {contt}| Response from VerifyMe - {JsonConvert.SerializeObject(_resp)}  - {DateTime.Now.ToString()}");

                if (_resp.status != "success")
                {
                    responseHandler.StatusCode = 99;
                    responseHandler.Message = _resp.message + ":" + _resp.code;
                    //LowRiskAssignment(res, _resp.message);
                }
                else if (_resp.status == "success")
                {
                    responseHandler.StatusCode = (int)HttpStatusCode.OK;
                    responseHandler.Message = _resp.message + ":" + _resp.code;
                    isAddressSent = true;
                }
                if (SkipRemark) 
                { 
                    var getFormattedRemarks = Remarks.FormatNewRemark($"Request to VerifyMe - CustomerId {sv.CustomerId}", "VerifyMe (Manual Reassignment)", sv.ActivityId, $"Request - {JsonConvert.SerializeObject(contt)}", $"AddresReassignment Response Code- {_resp.status}:{_resp.message}");
                //insert remarks
                    _rem.Insert(getFormattedRemarks);
                 }
            }
            catch (Exception e)
            {
                Console.WriteLine($"AddressReAssignment:{nameof(PushCustomerToVerifyMe)} AddressAssignedToVendorFailed:{res.CIF}  Request: {res.ToJson()} EXCEPTION| Error :{e.StackTrace}|- {e.InnerException} |- {e.Message}");
                //customer failed to register at you verify
                responseHandler.StatusCode = (int)HttpStatusCode.InternalServerError;
                responseHandler.Message = "Address saved as low risk";
               
            }
            return responseHandler;
        }

        private string FormatPhoneNumber(string ph)
        {
            string number = "";
            try
            {
                ph = ph.Replace("(", "").Replace(")", "").Replace("+", "");

                if (ph.StartsWith("234"))
                {
                    number = "0" + ph.Substring(3);
                }
                else if (ph.StartsWith("+234"))
                {
                    number = "0" + ph.Substring(4);
                }
                else if (ph.StartsWith("(+234)"))
                {
                    number = "0" + ph.Substring(6);
                }
                else if (ph.StartsWith("(234)"))
                {
                    number = "0" + ph.Substring(5);
                }
                else
                {
                    number = ph;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"FormatPhoneNumber - {e.StackTrace}");
            }
            return number;
        }


        
        public Customer FormatExistingCustomerRecord(Customer c)
        {
            Customer customer = null;
            try
            {
                customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    IsExistingCustomer = c.IsExistingCustomer,
                    ChannelId = c.ChannelId,
                    FirstName = c.FirstName,//will come now from db
                    LastName = c.LastName,//will come now from db
                    Bvn = c.Bvn.ToLower() == "n/a" ? "" : c.Bvn,
                    AccountNumber = c.AccountNumber, //QueryFinnacle.GetFirstLinkedAccountByCIF(c.CIF, connString),//will come now from db
                    CIF = c.CIF,
                    DateCreated = DateTime.Now,
                    Email = c.Email,
                    MobileNumber = c.MobileNumber,
                    ProfileId = c.ProfileId,
                    BranchId = c.BranchId,//QueryFinnacle.GetSolIdNameByCIFExistingCustomers(c.CIF, connString),//will come now from db
                    ClientId = c.ChannelId ?? c.ClientId,
                    DOB = c.DOB,
                    BvnAddress = c.BvnAddress,
                    BvnBase64Image = c.BvnBase64Image,
                    Nationality = c.Nationality,
                    ReferalCode = c.ReferalCode,
                    Address = c.Address,
                    IsIDCardApproved = c.IsIDCardApproved,
                    IsMigrated = c.IsMigrated,
                    IsResidentialAddressApproved = c.IsResidentialAddressApproved,
                    IsSelfieApproved = c.IsSelfieApproved,
                    IsSignatureApproved = c.IsSignatureApproved,
                    ApFlyerID = c.ApFlyerID,
                    AccountName = c.AccountName,
                    MiddleName = c.MiddleName,
                    SchemeCode = c.SchemeCode,
                    IsWallet = (c.SchemeCode == "64003" || c.SchemeCode == "64002") ? true : false
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(nameof(FormatExistingCustomerRecord) + " - " + e.StackTrace);

            }
            return customer;
        }

        private static string GetFirstName(string fullname)
        {
            try
            {
                if (fullname.Contains(" "))
                {
                    return fullname.Split(' ')[0];
                }
                else
                {
                    return fullname;
                }
            }
            catch (Exception)
            {
                return fullname;
            }
        }

        private static string GetLastName(string fullname)
        {
            try
            {
                if (fullname.Contains(" "))
                {
                    return fullname.Split(' ')[1];
                }
                else
                {
                    return "";
                }
            }
            catch (Exception)
            {
                return fullname;
            }
        }

        public string GetNextVendorForAddressVerification()
        {
            string nextEligibleVendorId = "";
            try
            {
                // select the id of the last vendor an address was assigned to from the AddressVerificationActivity table
                var lastAssignmentVendor = GetLastAssignmentVendorId();

                nextEligibleVendorId = GetNextEligibleVendorId(lastAssignmentVendor);
            }
            catch (Exception e)
            {
                _log.LogCritical($"{nameof(GetNextVendorForAddressVerification)}<<nextEligibleVendorId:{nextEligibleVendorId}  - ERROR MSG: {e.Message}|=>INNEREX:{e.InnerException} |=> StackTrace: {e.StackTrace}");
                _log.LogError($"{nameof(GetNextVendorForAddressVerification)} - {e.ToJson()}");
            }
            return nextEligibleVendorId;
        }

        public string GetNextEligibleVendorId(string lastAssignmentVendor)
        {
            string absVendorId = "";
            try
            {
                var resAdd = _addVendor.GetAll(x => x.IsEnabled);
                var vendorList = resAdd.Where(p => p.IsEnabled).Select(s => s.Id.ToString()).ToList();

                var nextVendor = NextAvailableVendorAlgo(vendorList, lastAssignmentVendor);
                absVendorId = PerformChecksV2(nextVendor);
            }
            catch (Exception e)
            {
                _log.LogError($"{nameof(GetNextEligibleVendorId)} - {e.ToJson()}");
            }
            return absVendorId;
        }


        public string GetLastAssignmentVendorId()
        {
            string lastVendorId = "";
            try
            {

                var res = _AddressVerification.GetRecentRecord(x => x.VendorId != "", p => p.DateCreated).Result;
                lastVendorId = res?.VendorId;
            }
            catch (Exception e)
            {
                Console.WriteLine($"{nameof(GetLastAssignmentVendorId)} ===> <><><> ERROR MSG:{e.Message}| INNEREx:{e.InnerException}|STACKTRACE:{e.StackTrace}|INNEREX:{e.InnerException}");
            }

            return lastVendorId;
        }

        private string NextAvailableVendorAlgo(List<string> vendorList, string lastAssignedVendor)
        {
            var vnd = vendorList.SkipWhile(x => x != lastAssignedVendor).Skip(1).DefaultIfEmpty(vendorList[0]).FirstOrDefault();
            return vnd;
        }

        public ResponseHandler<AddressVerificationVendor> GetVendor(Guid id)
        {
            try
            {
                return _addVendor.GetSingle(p => p.Id == id);
            }
            catch (Exception e)
            {
                Console.WriteLine($"AddressReAssignment:{nameof(GetVendor)} -  EXCEPTION| Error :{e.StackTrace}|- {e.InnerException} |- {e.Message}"); return null;
            }
        }

        private ResponseHandler PushCustomerToYouVerify(YouVerifyCandidateRequestModel req, ResidentialAddress res, Customer cus, AddressVerificationVendor vend, AddressVerificationActivity sv, bool SkipRemark = false)
        {
            var responseHandler = new ResponseHandler();
            bool isAddressSent = false;
            YouVerifyCandidateResponseModel _resp = null;
            YouVerifyAddressVerificationResponseModel _addResp = null;
            HttpClient _cli = new HttpClient();
            try
            {
                string content = JsonConvert.SerializeObject(req);

                _cli.DefaultRequestHeaders.Add("token", vend.AppKey);

                //register the customer
                HttpResponseMessage result = _cli.PostAsync($"{vend.BaseUrl}v1/candidates", new StringContent(content, Encoding.UTF8, "application/json")).Result;
                var re = Encoding.UTF8.GetString(result.Content.ReadAsByteArrayAsync().Result);
                _resp = JsonConvert.DeserializeObject<YouVerifyCandidateResponseModel>(re);

                if (_resp.status_code == (int)HttpStatusCode.OK)
                {
                    //register youverify's response on our db (with permission from bosses, cos this can make resource usage very large)
                    //then initiate address verification request from them

                    var img = "https://www.wemabank.com/wp-content/uploads/2020/05/wemlog.png";

                    var addReq = new YouVerifyAddressVerificationRequestModel
                    {
                        description = res.FullAddress,
                        address = new Address
                        {
                            building_number = res.BuildingNumber,
                            building_name = res.Apartment,
                            city = res.City,
                            country = res.Country != "Nigeria" ? "Nigeria" : res.Country,
                            flat_number = res.BuildingNumber,
                            landmark = res.Landmark,
                            postcode = "",
                            state = res.State.ToLower().Contains("state") ? res.State.Remove(res.State.IndexOf(' ')) : res.State,
                            street = res.Street,
                            sub_street = res.Street
                        },
                        images = new List<string>
                        {
                            img
                        }
                    };

                    string contt = JsonConvert.SerializeObject(addReq);

                    HttpResponseMessage resu = _cli.PostAsync($"{vend.BaseUrl}v1/candidates/{_resp.data.id}/live_photo", new StringContent(contt, Encoding.UTF8, "application/json")).Result;
                    var r = Encoding.UTF8.GetString(resu.Content.ReadAsByteArrayAsync().Result);
                    _addResp = JsonConvert.DeserializeObject<YouVerifyAddressVerificationResponseModel>(r);
                    responseHandler.StatusCode = _addResp.status_code;
                    responseHandler.Message = _addResp.message;
                    if (_addResp.status_code != (int)HttpStatusCode.OK || _addResp.status_code != 0 || _addResp.status_code != 00)
                    {
                        // LowRiskAssignment(res, _addResp.message);
                    }
                    else if (_addResp.status_code == (int)HttpStatusCode.OK || _addResp.status_code == 0)
                    {
                        isAddressSent = true;
                        responseHandler.StatusCode = (int)HttpStatusCode.OK;
                        responseHandler.Message = _resp.message;
                    }
                    if (SkipRemark)
                    {
                        var getFormattedRemarks = Remarks.FormatNewRemark($"Request to YouVerify - CustomerId {sv.CustomerId}", "YouVerify (Manual Reassignment)", sv.ActivityId, $"Request - {JsonConvert.SerializeObject(contt)}", $"AddresReassignment Response Code- {_addResp.status_code}:{_addResp.message}");
                        //insert remarks
                        _rem.Insert(getFormattedRemarks);
                    }
                }
                else
                {
                    responseHandler.StatusCode = (int)HttpStatusCode.OK;
                    responseHandler.Message = "Address saved as low risk";
                    Console.WriteLine($"AddressReAssignment:{nameof(PushCustomerToYouVerify)} WhenValidationFails:{_resp.status_code}| CIF:{res.CIF}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"AddressReAssignment:{nameof(PushCustomerToYouVerify)} AddressAssignedToVendorFailed:{res.CIF}  Request: {res.ToJson()} EXCEPTION| Error :{e.StackTrace}|- {e.InnerException} |- {e.Message}");
                responseHandler.StatusCode = (int)HttpStatusCode.Forbidden;
                responseHandler.Message = "An error occurred while pushing to vendor's api";
            }

            return responseHandler;
        }

        private ResponseHandler PushCustomerToSalisu(ResidentialAddress res, Customer cus, AddressVerificationVendor vend, AddressVerificationActivity sv, bool SkipRemark=false)
        {
            var responseHandler = new ResponseHandler();
            TokenModel _resp = null;
            HttpClient _cli = new HttpClient();
            bool isAddressSent = false;
            try
            {
                Dictionary<string, string> _use = new Dictionary<string, string>();
                _use.Add("grant_type", "password");
                _use.Add("username", _config.Value.SalisuUN);
                _use.Add("password", _config.Value.SalisuPW);

                //get access token
                HttpResponseMessage result = _cli.PostAsync($"{vend.BaseUrl}api/oauth2/token", new FormUrlEncodedContent(_use)).Result;
                var re = Encoding.UTF8.GetString(result.Content.ReadAsByteArrayAsync().Result);
                _resp = JsonConvert.DeserializeObject<TokenModel>(re);

                if (String.IsNullOrEmpty(_resp.error))
                {
                    //then initiate address verification request from them
                    var addReq = new SalisuAddressVerificationRequest
                    {
                        CustReferenceNo = sv.ActivityId.ToString(),
                        FullName = cus.FirstName + " " + cus.MiddleName + " " + cus.LastName,
                        City = res.City,
                        State = res.State,
                        CustomerAddress = res.FullAddress
                    };
                    if (string.IsNullOrEmpty(addReq.FullName))
                    {
                        isAddressSent = false;
                        responseHandler.StatusCode = (int)HttpStatusCode.BadRequest;
                        responseHandler.Message = "Customer name is required!";
                        return responseHandler;
                    }


                    _cli.DefaultRequestHeaders.Add("Authorization", "Bearer " + _resp.access_token);

                    string contt = JsonConvert.SerializeObject(addReq);
                    Console.WriteLine($"AddressReAssignment:{nameof(PushCustomerToSalisu)} Request Object => {contt}");
                    _log.LogInformation($"AddressReAssignment:{nameof(PushCustomerToSalisu)} Request Object => {contt}");

                    HttpResponseMessage resu = _cli.PostAsync($"{vend.BaseUrl}CustAddrInfo/CustInfoRequest", new StringContent(contt, Encoding.UTF8, "application/json")).Result;
                    var r = Encoding.UTF8.GetString(resu.Content.ReadAsByteArrayAsync().Result);
                    _resp = JsonConvert.DeserializeObject<TokenModel>(r);
                    Console.WriteLine($"AddressReAssignment:{nameof(PushCustomerToSalisu)} Response Object => {JsonConvert.SerializeObject(_resp)}");
                    _log.LogInformation($"AddressReAssignment:{nameof(PushCustomerToSalisu)} Response Object => {JsonConvert.SerializeObject(_resp)}");
                    try
                    {

                        if (_resp.StatusCode == "00" || _resp.StatusCode == "0")
                        {
                            isAddressSent = true;
                            responseHandler.StatusCode = (int)HttpStatusCode.OK;
                            responseHandler.Message = _resp.StatusMessage;

                        }
                        else if (_resp != null && _resp.StatusCode != "0" && _resp.StatusCode != "00")
                        {
                            isAddressSent = false;
                            responseHandler.StatusCode = Convert.ToInt16(_resp.StatusCode);
                            responseHandler.Message = _resp.StatusMessage;
                        }

                    }
                    catch { }
                    if (SkipRemark)
                    {
                        var getFormattedRemarks = Remarks.FormatNewRemark($"Request to Salisu - CustomerId {sv.CustomerId}", "Salisu (Manual Reassignment)", sv.ActivityId, $"Request - {JsonConvert.SerializeObject(contt)}", $"AddresReassignment Response Code- {_resp.StatusCode}:{_resp.StatusMessage}");

                        //insert remarks
                        _rem.Insert(getFormattedRemarks);
                    }
                }
                else
                {
                    //Console.WriteLine($"Response from Salisu On Auth - {JsonConvert.SerializeObject(_resp)}");
                    responseHandler.StatusCode = (int)HttpStatusCode.OK;
                    responseHandler.Message = "Address saved as low risk";
                    Console.WriteLine($"AddressReAssignment:{nameof(PushCustomerToSalisu)} WhenValidationFails:{_resp.error}|{_resp.StatusCode}| CIF:{res.CIF}|{sv.CustomerId}");
                }
            }
            catch (Exception e)
            {
                //Console.WriteLine($"PushCustomerToSalisu Exception - {e}");
                Console.WriteLine($"AddressReAssignment:{nameof(PushCustomerToSalisu)} AddressAssignedToVendorFailed:{res.CIF}  Request: {res.ToJson()} EXCEPTION| Error :{e.StackTrace}|- {e.InnerException} |- {e.Message}");
                responseHandler.StatusCode = (int)HttpStatusCode.Forbidden;
                responseHandler.Message = "An error occurred while pushing to vendor (Salisu) api!";
            }

            return responseHandler;
        }

        private ResponseHandler PushCustomerToOnigbanjo(ResidentialAddress res, Customer cus, AddressVerificationVendor vend, AddressVerificationActivity sv, bool skipRemark=false, string VendorClientId="", string Published="1")
        {//Sunday:method added when Onigbanjo released their api -24-Aug-2023
            //getting method not allowed error
            var responseHandler = new ResponseHandler();
            OnigbanjoResponse _resp = null;
            OnigbanjoFailureResponse _respError = null;
            HttpClient _cli = new HttpClient();
            bool isAddressSent = false;
            try
            {
                //then initiate address verification request for them
                var addReq = new OnigbanjoAddressVerificationRequest
                {
                    clientJobrefID = sv.ActivityId.ToString(),
                    ref_no = Guid.NewGuid().ToString(),
                    first_name = cus.FirstName,
                    last_name = cus.LastName,
                    state = res.State,
                    city = res.City,
                    phone_number = cus.MobileNumber,
                    email = cus.Email,
                    address = res.FullAddress,
                    client = VendorClientId,//_config.Value.VendorClientId,
                    published = Published // _config.Value.Published
                };

                if (string.IsNullOrEmpty(addReq.first_name) || string.IsNullOrEmpty(addReq.address))
                {
                    isAddressSent = false;
                    responseHandler.StatusCode = (int)HttpStatusCode.BadRequest;
                    responseHandler.Message = "Customer name/address is required!";
                    return responseHandler;
                }

                //_cli.DefaultRequestHeaders.Add("Authorization", "Bearer " + _resp.access_token);

                string contt = JsonConvert.SerializeObject(addReq);
                Console.WriteLine($"BOAddress:{nameof(PushCustomerToOnigbanjo)} Request Object => {contt}");
                // _log.LogInformation($"BOAddress:{nameof(PushCustomerToOnigbanjo)} Request Object => {contt}");

                HttpResponseMessage resu = _cli.PostAsync($"{vend.BaseUrl}jobs/{VendorClientId}", new StringContent(contt, Encoding.UTF8, "application/json")).Result;
                var r = Encoding.UTF8.GetString(resu.Content.ReadAsByteArrayAsync().Result);
                Console.WriteLine($"BOAddress:{nameof(PushCustomerToOnigbanjo)} Whole Response Object => {r}");
                // _log.LogInformation($"BOAddress:{nameof(PushCustomerToOnigbanjo)} Whole Response Object => {r}");

                try
                {
                    if (!string.IsNullOrEmpty(r) && r.ToLower().Contains("success"))
                    {
                        _resp = JsonConvert.DeserializeObject<OnigbanjoResponse>(r);
                        Console.WriteLine($"BOAddress:{nameof(PushCustomerToOnigbanjo)} Response Object => {JsonConvert.SerializeObject(_resp)}");
                        //_log.LogInformation($"BOAddress: {nameof(PushCustomerToOnigbanjo)} Response Object => {JsonConvert.SerializeObject(_resp)}");
                        isAddressSent = true;
                        responseHandler.StatusCode = (int)HttpStatusCode.OK;
                        responseHandler.Message = $"{_resp.status}|{_resp.data.jobs.ref_no}|{_resp.data.jobs.address}";
                    }
                    else
                    {
                        _respError = JsonConvert.DeserializeObject<OnigbanjoFailureResponse>(r);
                        Console.WriteLine($"BOAddress:{nameof(PushCustomerToOnigbanjo)} Error Response Object => {JsonConvert.SerializeObject(_respError)}");
                        // _log.LogInformation($"BOAddress: {nameof(PushCustomerToOnigbanjo)} Error Response Object => {JsonConvert.SerializeObject(_respError)}");
                        isAddressSent = false;
                        responseHandler.StatusCode = 99;
                        responseHandler.Message = $"{_respError.status}|{_respError.message.non_field_errors[0]}";
                    }
                }
                catch (Exception e)
                {
                    responseHandler.StatusCode = (int)HttpStatusCode.Forbidden;
                    responseHandler.Message = "An error occurred while processing Vendor's (Onigbanjo) Response from api! MSG:" + e.Message;
                    Console.WriteLine($"BOAddress:{nameof(PushCustomerToOnigbanjo)} Error MSG => {responseHandler.Message}");
                    //_log.LogInformation($"BOAddress: {nameof(PushCustomerToOnigbanjo)} Error MSG => {responseHandler.Message}");
                }

                if (skipRemark) 
                { 
                    var getFormattedRemarks = Remarks.FormatNewRemark($"Request to Onigbanjo - CustomerId {sv.CustomerId}", "Onigbanjo (Manual Reassignment)", sv.ActivityId, $"Request - {JsonConvert.SerializeObject(contt)}", $"Response - {r}");

                    //insert remarks
                    _rem.Insert(getFormattedRemarks);
                  }

            }
            catch (Exception e)
            {
                //Console.WriteLine($"PushCustomerToSalisu Exception - {e}");
                Console.WriteLine($"BOAddress:{nameof(PushCustomerToOnigbanjo)} AddressAssignedToVendorFailed:{res.CIF}  Request: {res.ToJson()} EXCEPTION| Error :{e.StackTrace}|- {e.InnerException} |- {e.Message}");
                //LowRiskAssignment(res, e.Message);
                responseHandler.StatusCode = (int)HttpStatusCode.Forbidden;
                responseHandler.Message = "An error occurred while pushing to vendor (Onigbanjo) api!";
            }

            return responseHandler;
        }


        private string SendEmail(MailModel mail)
        {
            string _resp = null;
            HttpClient _cli = new HttpClient();
            try
            {
                string content = JsonConvert.SerializeObject(mail);

                _cli.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _config.Value.ApiKey);

                //var url = _config["baseUrl"];
                HttpResponseMessage result = _cli.PostAsync(_config.Value.EmailBaseUrl, new StringContent(content, Encoding.UTF8, "application/json")).Result;

                _resp = Encoding.UTF8.GetString(result.Content.ReadAsByteArrayAsync().Result);
            }
            catch (Exception e)
            {
                //Console.WriteLine(nameof(SendEmail) + " - " + e);
                _resp = e.Message;
            }
            return _resp;
        }

        private string PerformChecksV2(string vendorsId)
        {
            string vendorId = "";
            try
            {
                var vId = Guid.Parse(vendorsId);
                var obj = _addVendor.GetSingle(x => x.Id == vId).SingleResult;
                //is vendor Enabled
                if (obj.IsEnabled)
                {
                    //further checks
                    //check if the vendor's turn around time is within reach
                    //to do that, let's get today's date
                    string day = DateTime.Now.DayOfWeek.ToString();

                    //now get the vendor's turn around time
                    long turnAroundTime = obj.TurnAroundTimeInDays;
                    if (turnAroundTime > 0)
                    {
                        //check what day the turn around time falls in
                        var dateOfTurnAroundTime = DateTime.Now.AddDays(turnAroundTime);
                        var dayOfTurnAroundTime = dateOfTurnAroundTime.DayOfWeek.ToString();

                        //check if day falls on a weekend
                        if (dayOfTurnAroundTime.ToLower() == "saturday" || dayOfTurnAroundTime.ToLower() == "sunday" || dayOfTurnAroundTime.ToLower() == "monday")
                        {
                            //check if vendor works on weekend
                            if (obj.WorksOnWeekend)
                            {
                                // you can return this vendor id
                                vendorId = obj.Id.ToString();
                            }
                        }
                        else
                        {
                            PublicHoliday nextPublicHoliday = null;
                            // turnAround time falls on a weekday
                            //get the next public holiday
                            var todayDate = DateTime.Now;
                            var rrer = _publicHol.GetAll(p => p.StartDate > todayDate);

                            if (rrer.Any())
                            {
                                nextPublicHoliday = rrer.ElementAt(0);

                                //check if the next public holiday is within reach of the turn around time
                                if (dateOfTurnAroundTime < nextPublicHoliday.StartDate && dateOfTurnAroundTime < nextPublicHoliday.FinishDate)
                                {
                                    // turn around time does not fall on a public holiday
                                    // now check if turn around time is reacheable on Friday of that week. Note maximum turn around time for vendors is 3 days
                                    if (obj.TurnAroundTimeInDays == 3 && (dayOfTurnAroundTime == "Friday" || dayOfTurnAroundTime == "Thursday"))
                                    {
                                        // vendor's turn around time falls on a weekly delivery date
                                        vendorId = obj.Id.ToString();
                                    }
                                    else if (obj.TurnAroundTimeInDays == 2 && (dayOfTurnAroundTime == "Friday" || dayOfTurnAroundTime == "Thursday" || dayOfTurnAroundTime == "Wednesday"))
                                    {
                                        // vendor's turn around time falls on a weekly delivery date
                                        vendorId = obj.Id.ToString();
                                    }
                                    else if (obj.TurnAroundTimeInDays == 1 && (dayOfTurnAroundTime == "Friday" || dayOfTurnAroundTime == "Thursday" || dayOfTurnAroundTime == "Wednesday" || dayOfTurnAroundTime == "Tuesday"))
                                    {
                                        // vendor's turn around time falls on a weekly delivery date                                              
                                        vendorId = obj.Id.ToString();
                                    }
                                    else
                                    {
                                        // vendor's turn around time does not fall on a weekly delivery date
                                    }
                                }
                                else
                                {
                                    // turn around time falls on a public holiday
                                    // check if the vendor works on public holiday
                                    if (obj.WorksOnPublicHoliday)
                                    {
                                        vendorId = obj.Id.ToString();
                                    }
                                }
                            }
                            else
                            {
                                vendorId = obj.Id.ToString();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"InBranchAddress: {nameof(PerformChecksV2)} ERROR-MSG: {e.Message}|STACKTRACE:{e.StackTrace}");
            }
            return vendorId;
        }

        public void ReviewAndProcessDimec(IConfiguration configuration)
        {
            throw new NotImplementedException();
        }
    }
}




