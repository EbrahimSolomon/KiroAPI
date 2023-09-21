using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Kiron_Interactive.CachingLayer;
using Kiron_Interactive.Data_Layer.Helpers;
using KironAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace KironAPI.Controllers
{
[ApiController]
[Route("api/bankholidays")]
public class BankHolidayController : ControllerBase
{
    private const string BANK_HOLIDAYS_URL = "https://www.gov.uk/bank-holidays.json";
    private readonly string _connectionString;
    private readonly CacheManager _cacheManager;
    private readonly HttpClient _httpClient;
    private static readonly object _syncLock = new object();
    private static SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);
    public BankHolidayController(IOptions<DatabaseSettings> dbSettings, CacheManager cacheManager, HttpClient httpClient)
    {
        _connectionString = dbSettings.Value.DefaultConnection;
        _cacheManager = cacheManager;
        _httpClient = httpClient;
    }
    
    [HttpGet("fetchUKBankHolidays")]
    public async Task<IActionResult> FetchUKBankHolidays()
    {
        using (var connection = DBConnectionManager.GetOpenConnection(_connectionString))
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT AutomatedProcessFlag FROM Configuration WHERE ConfigId = 1";
            bool isAutomatedProcessFlagSet = (bool)command.ExecuteScalar();

            if (isAutomatedProcessFlagSet)
            {
                return BadRequest("The work for this endpoint has been fulfilled.");
            }

            // Fetch data from the given URL.
            var data = await CallExternalURL();

            // Parse and save to DB
            SaveToDB(data, connection);

            // Set the automated process flag.
            command.CommandText = "UPDATE Configuration SET AutomatedProcessFlag = 1 WHERE ConfigId = 1";
            command.ExecuteNonQuery();
        }

        return Ok("Data saved successfully.");
    }

   private async Task<JObject> CallExternalURL()
{
    var response = await _httpClient.GetStringAsync(BANK_HOLIDAYS_URL);
    return JObject.Parse(response);
}

private void SaveToDB(JObject data, IDbConnection connection)
{
    foreach (var regionProperty in data.Properties())
    {
        string regionName = regionProperty.Name;
        var holidays = regionProperty.Value["events"].ToObject<List<HolidayEvent>>();

        // Check if the region exists already
        var checkRegionCmd = connection.CreateCommand();
        checkRegionCmd.CommandText = "SELECT RegionID FROM Regions WHERE RegionName = @RegionName";
        checkRegionCmd.Parameters.Add(new SqlParameter("@RegionName", regionName));
        var existingRegionIdObj = checkRegionCmd.ExecuteScalar();

        int regionId;
        if (existingRegionIdObj == null)
        {
            var insertRegionCmd = connection.CreateCommand();
            insertRegionCmd.CommandText = "INSERT INTO Regions (RegionName) VALUES (@RegionName); SELECT SCOPE_IDENTITY();";
            insertRegionCmd.Parameters.Add(new SqlParameter("@RegionName", regionName));
            regionId = Convert.ToInt32(insertRegionCmd.ExecuteScalar());
        }
        else
        {
            regionId = Convert.ToInt32(existingRegionIdObj);
        }

        foreach (var holiday in holidays)
        {
            // Check if the holiday exists already
            var checkHolidayCmd = connection.CreateCommand();
            checkHolidayCmd.CommandText = "SELECT HolidayID FROM Holidays WHERE HolidayDate = @Date AND HolidayName = @Name";
            checkHolidayCmd.Parameters.Add(new SqlParameter("@Date", holiday.Date));
            checkHolidayCmd.Parameters.Add(new SqlParameter("@Name", holiday.Title));
            var existingHolidayIdObj = checkHolidayCmd.ExecuteScalar();

            int holidayId;
            if (existingHolidayIdObj == null)
            {
                var insertHolidayCmd = connection.CreateCommand();
                insertHolidayCmd.CommandText = "INSERT INTO Holidays (HolidayName, HolidayDate) VALUES (@Name, @Date); SELECT SCOPE_IDENTITY();";
                insertHolidayCmd.Parameters.Add(new SqlParameter("@Name", holiday.Title));
                insertHolidayCmd.Parameters.Add(new SqlParameter("@Date", holiday.Date));
                holidayId = Convert.ToInt32(insertHolidayCmd.ExecuteScalar());
            }
            else
            {
                holidayId = Convert.ToInt32(existingHolidayIdObj);
            }

            // Check if the relationship between region and holiday already exists
            var checkRegionHolidayCmd = connection.CreateCommand();
            checkRegionHolidayCmd.CommandText = "SELECT RegionHolidayID FROM RegionHolidays WHERE RegionID = @RegionID AND HolidayID = @HolidayID";
            checkRegionHolidayCmd.Parameters.Add(new SqlParameter("@RegionID", regionId));
            checkRegionHolidayCmd.Parameters.Add(new SqlParameter("@HolidayID", holidayId));
            var existingRegionHolidayIdObj = checkRegionHolidayCmd.ExecuteScalar();

            if (existingRegionHolidayIdObj == null)
            {
                // Insert into RegionHolidays table
                var insertRegionHolidayCmd = connection.CreateCommand();
                insertRegionHolidayCmd.CommandText = "INSERT INTO RegionHolidays (RegionID, HolidayID) VALUES (@RegionID, @HolidayID)";
                insertRegionHolidayCmd.Parameters.Add(new SqlParameter("@RegionID", regionId));
                insertRegionHolidayCmd.Parameters.Add(new SqlParameter("@HolidayID", holidayId));
                insertRegionHolidayCmd.ExecuteNonQuery();
            }
        }
    }
}

[HttpGet]
public IActionResult ScheduleJob()
{
    RecurringJob.AddOrUpdate("FetchUKBankHolidaysJob", () => FetchUKBankHolidays(), Cron.Daily); 
    // This schedules the FetchUKBankHolidays method to run daily.
    return Ok("Job Scheduled");
}

[HttpGet("regions")]
public async Task<IActionResult> GetRegions()
{
    const string cacheKey = "AllRegions";
    List<string> allRegions;

    await _asyncLock.WaitAsync();
    try
    {
        if (!_cacheManager.Contains(cacheKey))
        {
            using (var executor = new CommandExecutor(_connectionString))
            {
                allRegions = (await executor.ExecuteStoredProcedureGetListAsync<string>("GetAllRegions")).ToList();
                _cacheManager.Add(cacheKey, allRegions, TimeSpan.FromMinutes(30));
            }
        }
        else
        {
            allRegions = _cacheManager.Get<List<string>>(cacheKey);
        }
    }
    finally
    {
        _asyncLock.Release();
    }

    return Ok(allRegions);
}

[HttpGet("holidays/{regionName}")]
public async Task<IActionResult> GetHolidaysByRegion(string regionName)
{
    string cacheKey = $"HolidaysForRegion_{regionName}";
    List<HolidayEvent> holidaysForRegion;

    await _asyncLock.WaitAsync();
    try
    {
        if (!_cacheManager.Contains(cacheKey))
        {
            using (var executor = new CommandExecutor(_connectionString))
            {
                var parameters = new { RegionName = regionName };
                holidaysForRegion = (await executor.ExecuteStoredProcedureGetListAsync<HolidayEvent>("GetHolidaysByRegion", parameters)).ToList();
                _cacheManager.Add(cacheKey, holidaysForRegion, TimeSpan.FromMinutes(30));
            }
        }
        else
        {
            holidaysForRegion = _cacheManager.Get<List<HolidayEvent>>(cacheKey);
        }
    }
    finally
    {
        _asyncLock.Release();
    }

    return Ok(holidaysForRegion);
}




    }
    
}
