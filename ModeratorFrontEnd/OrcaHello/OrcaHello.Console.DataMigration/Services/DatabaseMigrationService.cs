using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using OrcaHello.Console.DataMigration.Models;
using System.Collections.ObjectModel;

namespace OrcaHello.Console.DataMigration.Services
{
    public class DatabaseMigrationService
    {
        private IConfigurationRoot _config;

        public DatabaseMigrationService(IConfigurationRoot config)
        {
            _config = config;
        }

        public void ShowConfiguration()
        {
            System.Console.WriteLine($"Cosmos DB Connection: {_config[AppSettings.CosmosDb]}");
            System.Console.WriteLine($"Cosmos DB Emulator Connect: {_config[AppSettings.LocalDb]}");
            System.Console.WriteLine($"Source DB Name: {_config[AppSettings.SourceDbName]}");
            System.Console.WriteLine($"Source Container Name: {_config[AppSettings.SourceContainerName]}");
            System.Console.WriteLine($"Source Partition Key: {_config[AppSettings.SourcePartitionKey]}");
            System.Console.WriteLine($"Target DB Name: {_config[AppSettings.TargetDbName]}");
            System.Console.WriteLine($"Target Container Name: {_config[AppSettings.TargetContainerName]}");
            System.Console.WriteLine($"Target Partition Key: {_config[AppSettings.TargetPartitionKey]}");
            PressAnyKey();
        }

        public async Task CopyDatabaseFromOnlineToLocal()
        {
            System.Console.WriteLine("Copying existing database from online to local emulator.");

            CosmosClient onlineClient = new CosmosClient(_config[AppSettings.CosmosDb]);
            CosmosClient localClient = new CosmosClient(_config[AppSettings.LocalDb]);

            // Get online container
            var onlineContainer = onlineClient.GetContainer(_config[AppSettings.SourceDbName],
                _config[AppSettings.SourceContainerName]);

            var copyCount = await CountTotalRecords(onlineContainer);
            System.Console.WriteLine($"Found {copyCount} records in {_config[AppSettings.SourceContainerName]} online to copy...");

            // Get or create local database and container
            Database localDatabase = await localClient.
                CreateDatabaseIfNotExistsAsync(_config[AppSettings.SourceDbName]);
            Container localContainer = await localDatabase.
                CreateContainerIfNotExistsAsync(_config[AppSettings.SourceContainerName],
                _config[AppSettings.SourcePartitionKey]);

            // Query data from online container
            var query = new QueryDefinition("SELECT * FROM c");
            var queryIterator = onlineContainer.GetItemQueryIterator<Metadata>(query);

            int recordCount = 0;

            while (queryIterator.HasMoreResults)
            {
                FeedResponse<Metadata> response = await queryIterator.ReadNextAsync();

                foreach (var item in response)
                {
                    recordCount++;
                    System.Console.WriteLine($"Copying record #{recordCount}");

                    // Get partition key value
                    PartitionKey partitionKey = new PartitionKey(item.source_guid); // Adjust property name

                    // Insert data into local container
                    await localContainer.CreateItemAsync(item, partitionKey);
                }
            }

            System.Console.WriteLine($"Finished copying {recordCount} records to {_config[AppSettings.SourceContainerName]} in emulator.");
            PressAnyKey();
        }

        public async Task CreateNewSchemaContainerOnLocal()
        {
            System.Console.WriteLine("Creating new container with updated schema on local emulator.");

            CosmosClient localClient = new CosmosClient(_config[AppSettings.LocalDb]);

            // Get online container
            var sourceContainer = localClient.GetContainer(_config[AppSettings.SourceDbName],
                _config[AppSettings.SourceContainerName]);

            var migrateCount = await CountTotalRecords(sourceContainer);
            System.Console.WriteLine($"Found {migrateCount} records in {_config[AppSettings.SourceContainerName]} to migrate to {_config[AppSettings.TargetContainerName]}...");

            // Get or create local database and container
            Database targetDatabase = await localClient.
                CreateDatabaseIfNotExistsAsync(_config[AppSettings.TargetDbName]);
            Container targetContainer = await targetDatabase.
                CreateContainerIfNotExistsAsync(_config[AppSettings.TargetContainerName], _config[AppSettings.TargetPartitionKey]);

            // Query data from online container
            var query = new QueryDefinition("SELECT * FROM c");
            var queryIterator = sourceContainer.GetItemQueryIterator<Metadata>(query);

            int recordCount = 0;

            while (queryIterator.HasMoreResults)
            {
                FeedResponse<Metadata> response = await queryIterator.ReadNextAsync();

                foreach (var item in response)
                {
                    recordCount++;
                    System.Console.WriteLine($"Migrating record #{recordCount}");

                    // convert old schema to new schema
                    var newItem = new Metadata2();

                    newItem.id = item.id;
                    newItem.audioUri = item.audioUri;
                    newItem.imageUri = item.imageUri;
                    newItem.timestamp = item.timestamp;
                    newItem.location = item.location;
                    newItem.predictions = item.predictions;
                    newItem.whaleFoundConfidence = item.whaleFoundConfidence;
                    newItem.comments = item.comments;
                    newItem.moderator = item.moderator;
                    newItem.dateModerated = item.dateModerated;

                    // We are turning tags into a list in the schema so it can
                    // be parsed and indexed better

                    if (!string.IsNullOrWhiteSpace(item?.tags))
                    {
                        newItem.tags = item?.tags?.Split(";")?.ToList();
                    }

                    // We are creating a location name higher up to make it easier to
                    // index

                    // We are also renaming "Haro Strait" to "Orcasound Lab", but leaving the
                    // node_name unchanged

                    var name = item?.location?.name;

                    if (name == "Haro Strait")
                    {
                        name = "Orcasound Lab";
                    }

                    newItem.locationName = name;
                    newItem.location.name = name;

                    // We are moving to a single field to indicate the state of the
                    // item (Unreviewed, Positive, Negative, Unknown)

                    if (!item.reviewed)
                    {
                        newItem.state = "Unreviewed";
                    }

                    if (item.reviewed && item.SRKWFound == "yes")
                    {
                        newItem.state = "Positive";
                    }

                    if (item.reviewed && item.SRKWFound == "no")
                    {
                        newItem.state = "Negative";
                    }

                    if (item.reviewed && item.SRKWFound == "don't know")
                    {
                        newItem.state = "Unknown";
                    }

                    // We are creating a new partition key
                    PartitionKey partitionKey = new PartitionKey(newItem.state); // Adjust property name

                    // Insert data into local container
                    await targetContainer.CreateItemAsync(newItem, partitionKey);
                }
            }

            System.Console.WriteLine($"Finished migrating {recordCount} records to {_config[AppSettings.TargetContainerName]} in emulator.");
            PressAnyKey();
        }

        public async Task CreateCompositeIndexOnLocal()
        {
            System.Console.WriteLine("Creating the composite index on local emulator.");

            CosmosClient localClient = new CosmosClient(_config[AppSettings.LocalDb]);

            using (localClient = new CosmosClient(_config[AppSettings.LocalDb]))
            {
                var targetContainer = localClient.GetContainer(_config[AppSettings.TargetDbName],
                    _config[AppSettings.TargetContainerName]);

                await CreateCompositeIndexes(targetContainer);
            }

            System.Console.WriteLine($"Finished creating composite indexes for {_config[AppSettings.TargetContainerName]} in emulator.");
            PressAnyKey();
        }

        public async Task CopyContainerToOnline()
        {
            System.Console.WriteLine("Copying new schema container from local emulator to online.");

            CosmosClient onlineClient = new CosmosClient(_config[AppSettings.CosmosDb]);
            CosmosClient localClient = new CosmosClient(_config[AppSettings.LocalDb]);

            System.Console.WriteLine($"Creating container {_config[AppSettings.TargetContainerName]} in {_config[AppSettings.SourceDbName]} online...");

            // Get online database and create new container with new partition key
            Database onlineDatabase = onlineClient.GetDatabase(_config[AppSettings.SourceDbName]);
            Container onlineContainer = await onlineDatabase.
                CreateContainerIfNotExistsAsync(_config[AppSettings.TargetContainerName], _config[AppSettings.TargetPartitionKey]);

            System.Console.WriteLine("Creating composite indexes...");

            await CreateCompositeIndexes(onlineContainer);

            var localContainer = localClient.GetContainer(_config[AppSettings.TargetDbName], _config[AppSettings.TargetContainerName]);

            var copyCount = await CountTotalRecords(localContainer);
            System.Console.WriteLine($"Found {copyCount} records in {_config[AppSettings.TargetContainerName]} in local emulator to copy...");

            // Query data from online container
            var query = new QueryDefinition("SELECT * FROM c");
            var queryIterator = localContainer.GetItemQueryIterator<Metadata2>(query);

            int recordCount = 0;

            while (queryIterator.HasMoreResults)
            {
                FeedResponse<Metadata2> response = await queryIterator.ReadNextAsync();

                foreach (var item in response)
                {
                    recordCount++;
                    System.Console.WriteLine($"Copying record #{recordCount}");

                    // Get partition key value
                    PartitionKey partitionKey = new PartitionKey(item.state);

                    // Insert data into local container
                    await onlineContainer.CreateItemAsync(item, partitionKey);
                }
            }

            System.Console.WriteLine($"Finished copying {recordCount} records to {_config[AppSettings.TargetContainerName]} online.");
            PressAnyKey();
        }

        private async Task CreateCompositeIndexes(Container container)
        {
            List<CompositePath> index1 = new List<CompositePath> {
                new CompositePath()
                {
                    Path = _config[AppSettings.TargetPartitionKey],
                    Order = CompositePathSortOrder.Ascending
                },

                new CompositePath()
                {
                    Path = "/timestamp",
                    Order = CompositePathSortOrder.Descending
                }
            };

            List<CompositePath> index2 = new List<CompositePath> {
                new CompositePath()
                {
                    Path = _config[AppSettings.TargetPartitionKey],
                    Order = CompositePathSortOrder.Ascending
                },

                new CompositePath()
                {
                    Path = "/whaleFoundConfidence",
                    Order = CompositePathSortOrder.Descending
                }
            };

            List<CompositePath> index3 = new List<CompositePath> {
                new CompositePath()
                {
                    Path = _config[AppSettings.TargetPartitionKey],
                    Order = CompositePathSortOrder.Ascending
                },

                new CompositePath()
                {
                    Path = "/moderator",
                    Order = CompositePathSortOrder.Descending
                }
            };

            List<CompositePath> index4 = new List<CompositePath> {
                new CompositePath()
                {
                    Path = _config[AppSettings.TargetPartitionKey],
                    Order = CompositePathSortOrder.Ascending
                },

                new CompositePath()
                {
                    Path = "/dateModerated",
                    Order = CompositePathSortOrder.Descending
                }
            };

            List<CompositePath> index5 = new List<CompositePath> {
                new CompositePath()
                {
                Path = _config[AppSettings.TargetPartitionKey],
                    Order = CompositePathSortOrder.Ascending
                },

                new CompositePath()
                {
                    Path = "/locationName",
                    Order = CompositePathSortOrder.Ascending
                }
            };

            List<CompositePath> index6 = new List<CompositePath> {
                new CompositePath()
                {
                    Path = "/timestamp",
                    Order = CompositePathSortOrder.Descending
                },

                new CompositePath()
                {
                    Path = "/whaleFoundConfidence",
                    Order = CompositePathSortOrder.Descending
                },

                new CompositePath()
                {
                    Path = "/moderator",
                    Order = CompositePathSortOrder.Ascending
                },

                new CompositePath()
                {
                    Path = "/dateModerated",
                    Order = CompositePathSortOrder.Descending
                }
            };

            ContainerResponse containerResponse = await container.ReadContainerAsync();
            ContainerProperties containerProperties = containerResponse.Resource;
            IndexingPolicy indexingPolicy = containerProperties.IndexingPolicy;

            indexingPolicy.CompositeIndexes.Clear();

            // Add the composite index to the indexing policy
            indexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>(index1));
            indexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>(index2));
            indexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>(index3));
            indexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>(index4));
            indexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>(index5));
            indexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>(index6));

            // Replace the container with the updated indexing policy
            await container.
                ReplaceContainerAsync(new ContainerProperties(container.Id, _config[AppSettings.TargetPartitionKey])
                {
                    IndexingPolicy = indexingPolicy
                });
        }

        private async Task<int> CountTotalRecords(Container container)
        {
            QueryDefinition queryDefinition = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
            FeedIterator<int> feedIterator = container.GetItemQueryIterator<int>(queryDefinition);

            int count = 0;
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<int> response = await feedIterator.ReadNextAsync();
                count += response.FirstOrDefault();
            }

            return count;
        }

        private void PressAnyKey()
        {
            System.Console.WriteLine();
            System.Console.WriteLine("Press any key to continue...");
            var input = System.Console.ReadLine();
        }
    }
}
