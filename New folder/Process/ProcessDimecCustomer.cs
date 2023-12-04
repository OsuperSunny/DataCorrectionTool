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

namespace DimecUpdate.Process
{
    public class ProcessDimecCustomer : IProcessDimecCustomer
    {
        ILogger<ProcessDimecCustomer> _log;
        private readonly IOptions<KeyValues> _config;
        private readonly IHttpClientFactory _httpClientFactory;        
        private readonly IDataRepository<Customer> _customer;
        private readonly IDataRepository<FacialValidationDetails> _faceValidation;

        public ProcessDimecCustomer(IOptions<KeyValues> config, ILogger<ProcessDimecCustomer> log, IHttpClientFactory httpClientFactory, IDataRepository<Customer> customer, IDataRepository<FacialValidationDetails> faceValidation)
        {
            _config = config;
            _log = log;
            _httpClientFactory = httpClientFactory;
            _customer = customer;
            _faceValidation = faceValidation;

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

        //public static ResidentialAddress FromJson(string json)
        //{
        //    return Newtonsoft.Json.JsonConvert.DeserializeObject<ResidentialAddress>(json);
        //}

        public void ReviewAndProcessDimec(IConfiguration config)
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

                //ResidentialAddress myObject = FromJson(json);

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

                //ResidentialAddress myObject2 = FromJson(json);

                //var classA = myObject;
                //var classB = myObject2;
                //if (AreDifferent(classA, classB))
                //{
                //    // Console.WriteLine("The classes are different.");
                //}
                //else
                //{
                //    // Console.WriteLine("The classes are the same.");
                //}
                //working well
                                
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

            string fileName = "";
            try
            {
                string filePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), fileName);

                string p = AppDomain.CurrentDomain.BaseDirectory;

                fileName = ReadDataFromFile.GetFilesByExtension(p, ".csv", SearchOption.AllDirectories).First().ToString();

                if(string.IsNullOrEmpty(fileName))
                    fileName = ReadDataFromFile.GetFilesByExtension(p, ".xlsx", SearchOption.AllDirectories).First().ToString();
                Console.WriteLine($"DimecCorrection =====PROCESSING OPERATION Started!=============");
                //Get a list of cifs to check against
                DataTable DataList = ReadDataFromFile.ConvertExcel(fileName);
                Console.WriteLine($"DimecCorrection Found {DataList.Rows.Count} CIF|Email Row(s) in the Excel File file! ");
               
                Console.WriteLine($"DimecCorrection Found {DataList.Rows.Count} CIF|Email Row(s) in the Excel File for DimecId Reassignment Op|");
                //call this method to process Address Reassignment
                if(DataList?.Rows?.Count > 0)
                {
                    Console.WriteLine($"DimecCorrection Found {DataList?.Rows?.Count} Data Row(s) in the Excel File for Dimec Correction Op|  About to process details... ");
                    CreateDimecCorrectionActivity(DataList, ProcessStatus, remarkForSearch);
                    Console.WriteLine($"DimecCorrection Found {DataList?.Rows?.Count} Data Row(s) in the Excel File for Dimec Correction Op||Now COmpleted! ");
                }
                else
                {
                    Console.WriteLine($"DimecCorrection Found Nothing to Process! ");
                }
                    
                                
            } catch (Exception ex) { Console.WriteLine($"DimecCorrection>> ERROR While Accessing File: MSG|STrace - {ex.Message}|{ex.StackTrace}"); }
        }

        public ResponseHandler CreateDimecCorrectionActivity(DataTable DimecIdDataList, int status, string remarkKeyword = "")
        {            
            ResponseHandler<Customer> c = new ResponseHandler<Customer>();
            var resHandler = new ResponseHandler();
            string vendorName = string.Empty;
            int SuccessfulCount = 0;
            Console.WriteLine($"DimecCorrection Status:{status}|RemarkKeyword:{remarkKeyword} <==> About to Process:{DimecIdDataList?.Rows?.Count} Found...");
            //get customer//get adddress//get address activity
            foreach (DataRow customer in DimecIdDataList.Rows)
            {
                string CIForEmailorOthers = string.Empty;
                string DimecId = string.Empty;
                try
                {
                    if (customer == null)
                    {
                        resHandler.StatusCode = (int)HttpStatusCode.BadRequest;
                        resHandler.Message = "Customer data is required!";
                        Console.WriteLine($"DimecCorrection Cutsomer to Process- CIF|Email: {customer["email"]+""} No Customer data Found...");
                        continue;
                    }

                    try
                    {
                        if (status == 1)//use email
                        {
                            CIForEmailorOthers = customer["email"] + "";
                            DimecId = customer["id"] + "";
                        }
                        if (status == 2)//use cif
                        {
                            CIForEmailorOthers = customer["cif"] + "";
                            DimecId = customer["id"] + "";
                        }
                        if (status == 3)//use bvn
                        {
                            CIForEmailorOthers = customer["bvn"] + "";
                            DimecId = customer["id"] + "";
                        }
                    }
                    catch
                    {
                        CIForEmailorOthers = customer["CIF"] + "";
                        DimecId = customer["id"] + "";
                    }

                    if (string.IsNullOrEmpty(CIForEmailorOthers) || string.IsNullOrEmpty(DimecId))
                    {
                        Console.WriteLine($"DimecCorrection When CIF/Email/DimecId is missing in the file - Cutsomer to Process- CIF: {CIForEmailorOthers} | DimecId: {DimecId}...");
                        continue;
                    }
                                        

                    var customerData = _customer.GetSingle(x => x.Email == CIForEmailorOthers && x.CustomerDimecID ==null);

                    c = _customer.GetSingle(x => ((x.CIF !=null && x.CIF == CIForEmailorOthers) || (x.Email != null && x.Email== CIForEmailorOthers) || (x.Bvn != null && x.Bvn == CIForEmailorOthers)) && (x.CustomerDimecID == null));

                    if (c.SingleResult == null)
                    {
                        resHandler.StatusCode = (int)HttpStatusCode.BadRequest;
                        resHandler.Message = "Customer data is not found!";
                        Console.WriteLine($"DimecCorrection Cutsomer to Process- CIForEmail: {CIForEmailorOthers}|DimecId:{DimecId} No Customer Found...");
                        continue;
                    }
                    //when customer is found proceed
                    
                    c.SingleResult.CustomerDimecID = Guid.Parse(DimecId);

                    Console.WriteLine($"DimecCorrection Cutsomer to Process- CIForEmail: {CIForEmailorOthers}|DimecId:{DimecId} Customer Found >>>About to Update...");

                    var updateNow = _customer.Update(c.SingleResult);

                    if(updateNow.StatusCode == (int)HttpStatusCode.OK)
                    {
                        Console.WriteLine($"DimecCorrection Cutsomer to Process- CIF: {CIForEmailorOthers} Successfully updated...");
                        SuccessfulCount += 1;
                        try 
                        {
                            var d = DimecBase64ImageDataRetrieval(Guid.Parse(DimecId), c.SingleResult.Id).Result;
                            Console.WriteLine($"DimecCorrection (Processing Dimec Selfie Cutsomer to Process- CIF: {CIForEmailorOthers} After Successfully updated. Status:{d?.isSuccessful}|Message:{d?.message}|SelfieImageExists:{d?.SelfieImageExists}");
                  
                        }
                        catch(Exception e) 
                        {
                            Console.WriteLine($"DimecCorrection: Dimec4SelfieDataRetrieval ERROR {nameof(CreateDimecCorrectionActivity)}:{CIForEmailorOthers}  | MSG :{e.Message}|STrace- {e.StackTrace} |INNEREX- {e.InnerException}");
                        }
                        
                    }
                    else 
                    {
                        Console.WriteLine($"DimecCorrection Cutsomer to Process- CIF: {CIForEmailorOthers} Failed to update...");
                    }

                                        
                }
                catch (Exception e)
                {
                    Console.WriteLine($"DimecCorrection: ERROR {nameof(CreateDimecCorrectionActivity)}:{CIForEmailorOthers}  | MSG :{e.Message}|STrace- {e.StackTrace} |INNEREX- {e.InnerException}");
                    resHandler.Error = e;
                    resHandler.StatusCode = 500;
                    resHandler.Message =  $"An error occurred while processing! Error:{e.Message}";
                    
                }
            }

            Console.WriteLine($"DimecCorrection Cutsomer to Process- Total Records Found: {DimecIdDataList?.Rows?.Count} | Total Successful:{SuccessfulCount}");
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



        public async Task<DimecResponseData> DimecBase64ImageDataRetrieval(Guid? Dimecid, Guid custId)
        {

            var result = new DimecResponseData();
            string DimecBaseurl = _config.Value.DimecImageUrl;
            string DimecRefUrl = $"/Utility/selfie/retrieval?dimecID={Dimecid}";
            
            Console.WriteLine($"|~DimecRetrieval~| - About to retrieve from Dimecs Api:{Dimecid}|{custId}");
            var selfieData = _faceValidation.GetRecentRecord(x => x.CustomerDimecID != null && x.CustomerDimecID == Dimecid && x.DimecImage != null, x => x.DateCreated);

            if (selfieData != null && selfieData.Result !=null)
            {
                return new DimecResponseData
                {
                    imageData = new ImageData
                    {
                        base64string = Convert.ToBase64String(selfieData.Result.DimecImage),
                        url = selfieData.Result.SelfieUrl,
                    },
                    SelfieImageExists = true,
                    isSuccessful = true,
                    message = selfieData.Result.ResponseMessage
                };

            }
            HttpClient Client = _httpClientFactory.CreateClient();
            string Url = DimecBaseurl + DimecRefUrl;
            try
            {
                var request = await Client.GetAsync(Url);
                var response = await request.Content.ReadAsStringAsync();
                
                Console.WriteLine($"|~DimecRetrieval~| - Result from Dimecs Api:{JsonConvert.SerializeObject(response)}");
                if (request == null)
                {
                    return new DimecResponseData
                    {
                        imageData = null,
                        SelfieImageExists = false,
                        isSuccessful = false,
                        message = "No content"
                    };
                }

                var ResponseBody = JsonConvert.DeserializeObject<DimecResponseData>(response);
                var selfie = ResponseBody.imageData.base64string;
                var DimecBase64Byte = Encoding.ASCII.GetBytes(ResponseBody.imageData.base64string);


                if (ResponseBody != null && !string.IsNullOrEmpty(ResponseBody.imageData.base64string))
                {
                    var faceValidation = FacialValidationDetails.FormatFacialValidations2(ResponseBody, custId, Dimecid);
                    var insert = _faceValidation.Insert(faceValidation);
                    var DimecDetails = new DimecSelfieData
                    {
                        DimecId = Dimecid,
                        DimecImageBase64byte = DimecBase64Byte,
                        DimecUrl = ResponseBody?.imageData?.url,
                        DateCreated = ResponseBody?.imageData?.selfieVerificationData.dateCreated ?? DateTime.MinValue,
                        Responsemessage = $"{ResponseBody.message}",
                        CustomerId = custId,
                        IsImageIdentical = ResponseBody?.imageData?.selfieVerificationData?.evaluationResult.Equals("MATCH") ?? false
                    };
                    //var insert2 = _dimecSelfie.Insert(DimecDetails);

                    if (insert.StatusCode == (int)HttpStatusCode.OK)
                    {
                        result = new DimecResponseData
                        {
                            imageData = new ImageData
                            {
                                url = ResponseBody.imageData.url,
                                base64string = ResponseBody.imageData.base64string
                            },

                            SelfieImageExists = ResponseBody.SelfieImageExists,

                            message = ResponseBody.message,

                            isSuccessful = ResponseBody.isSuccessful,

                            errors = ResponseBody.errors


                        };
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"|~DimecRetrieval~|Error occurred while fetching Dimec Selfie Record-{Dimecid}-MSG:{ex.Message}|Ex: {ex.InnerException}");
                
            }
            return null;
        }



    }
}