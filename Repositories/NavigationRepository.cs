using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kiron_Interactive.Data_Layer.Helpers;
using KironAPI.Models;
using Microsoft.Extensions.Options;

namespace KironAPI.Repositories
{
    public class NavigationRepository
    {
        private readonly string _connectionString;

        public NavigationRepository(IOptions<DatabaseSettings> dbSettings)
        {
            _connectionString = dbSettings.Value.DefaultConnection;
        }

        public async Task<ServiceResponse<List<Navigation>>> GetNavigationHierarchy()
        {
            var response = new ServiceResponse<List<Navigation>>();
            try
            {
                using var commandExecutor = new CommandExecutor(_connectionString);
                var navItems = (await commandExecutor.ExecuteStoredProcedureGetListAsync<Navigation>("FetchNavigationItems")).ToList();


                // Group items by their ParentID for efficient lookups
                var lookup = navItems.ToLookup(item => item.ParentID);

                // Fetch top-level items
                var topLevelItems = lookup[-1].ToList();

                // Recursively build the hierarchy
                foreach (var item in topLevelItems)
                {
                    BuildHierarchy(item, lookup);
                }

                response.Data = topLevelItems;
                response.Success = true;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = "Error fetching navigation hierarchy.";
                response.Data = null; // or you can set it to the detailed error if you wish
            }

            return response;
        }

        private void BuildHierarchy(Navigation item, ILookup<int, Navigation> lookup)
        {
            try
            {
                // Add children to the current item
                item.Children.AddRange(lookup[item.ID]);

                // Recursively do the same for each child
                foreach (var child in item.Children)
                {
                    BuildHierarchy(child, lookup);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error building hierarchy for item {item.ID}: {ex.Message}");
            }
        }
    }
}
