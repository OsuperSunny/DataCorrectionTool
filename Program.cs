using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using DomainManager.Helper;
using DomainManager;
using DocumentAssignment.Process;
using CAFTDomain.Aggregate;
using DataRepository.Interface;
using DataRepository.Implementation;
using DataRepository.Entity;
using Microsoft.EntityFrameworkCore;
using DimecUpdate.Process;
using SharedKernel.Model;
using DomainManager.Interface;
using DimecUpdate;

namespace DimecUpdate
{
    class Program
    {
        private static IConfigurationSection config;
        static void Main(string[] args)
        {
            try
            {
                WriteLog.WriteLogToFile("App Started...");

                var builder = new ConfigurationBuilder();

                BuidConfig(builder);

                //Log.Logger = new LoggerConfiguration()
                //    //.ReadFrom.Configuration(builder.Build())
                //    .Enrich.FromContext().
                //    WriteTo.Console()
                //    .CreateLogger();

                //PressAnyKey();
                Console.WriteLine($"Time:{DateTime.Now} ===++*********************************************************************************++===");
                Console.WriteLine($"Time:{DateTime.Now} ===++*********************[ConsoleApp] APPLICATION STARTED*********************++===");
                WriteLog.WriteLogToFile($"Time:{DateTime.Now} ===++*********************[ConsoleApp] APPLICATION STARTED*********************++===");
                Log.Logger.Information($"Time:{DateTime.Now} ===++*********************************************************************************++===");
                Log.Logger.Information($"Time:{DateTime.Now} ===++*********************[ConsoleApp] APPLICATION STARTED*********************++===");

                var host = Host.CreateDefaultBuilder()
                 .ConfigureServices((context, services) =>
                 {
                     services.AddDbContext<DataDbContext>(option => option.UseSqlServer(context.Configuration.GetConnectionString("DigitalOperationContext"), builder =>
                     {
                         builder.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);

                     }));
                     config = context.Configuration.GetSection("AppSettings");

                     services.AddHttpClient();
                     services.AddTransient<IAzureService, AzureService>();
                     
                     services.AddTransient<IProcessDimecCustomer, ProcessDimecCustomer>();
                     services.AddTransient<IDataRepository<FacialValidationDetails>, DataRepository<FacialValidationDetails>>();
                     
                     services.AddTransient<Customer>();
                     services.AddTransient<CustomerDocument>();
                     
                     services.AddTransient<IDataRepository<CustomerDocument>, DataRepository<CustomerDocument>>();
                     services.AddTransient<IDataRepository<Customer>, DataRepository<Customer>>();
                     

                 })
                 .Build();


                // insert other console app code here

                Console.WriteLine($"Time:{DateTime.Now} [ConsoleApp] CUSTOMER ADDRESS REASSIGNMENT OPERATION BEGINS...");
                WriteLog.WriteLogToFile($"Time:{DateTime.Now} [ConsoleApp] CUSTOMER ADDRESS REASSIGNMENT OPERATION BEGINS...");
                var svc = ActivatorUtilities.CreateInstance<ProcessDimecCustomer>(host.Services);
                svc.ReviewAndProcessDimec(config);

                Console.WriteLine($"Time:{DateTime.Now} [ConsoleApp] END OF PROCESSING!");
                Console.WriteLine($"Time:{DateTime.Now} ===++***********************************************************++===");
            }catch(Exception e)
            {
                WriteLog.WriteLogToFile(e.ToString());
                Console.WriteLine(e.ToString());
            }

            //Console.ReadKey();

        }



        static void PressAnyKey()
        {
            if (GetConsoleProcessList(new int[2], 2) <= 1)
            {
                Console.Write("Press any key to continue");
                //Console.ReadKey();
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern int GetConsoleProcessList(int[] buffer, int size);


        static IHostBuilder CreateHostBuilder(string[] args) =>
           Host
               .CreateDefaultBuilder(args)
               .UseConsoleLifetime()
               .ConfigureServices(ConfigureServices);

        static void ConfigureServices(HostBuilderContext arg1, IServiceCollection arg2)
        {
            throw new NotImplementedException();
        }

        private static void BuidConfig(IConfigurationBuilder builder)
        {

            builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                    .AddEnvironmentVariables();


        }

        
    }
}