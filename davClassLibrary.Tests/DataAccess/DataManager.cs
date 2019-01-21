﻿using davClassLibrary.Common;
using davClassLibrary.Tests.Common;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using static davClassLibrary.Models.TableObject;

namespace davClassLibrary.Tests.DataAccess
{
    [TestFixture][SingleThreaded]
    class DataManager
    {
        #region Setup
        [OneTimeSetUp]
        public void GlobalSetup()
        {
            ProjectInterface.LocalDataSettings = new LocalDataSettings();
            ProjectInterface.RetrieveConstants = new RetrieveConstants();
            ProjectInterface.TriggerAction = new TriggerAction();
            ProjectInterface.GeneralMethods = new GeneralMethods();
        }

        [SetUp]
        public void Setup()
        {
            // Delete all files and folders in the test folder except the database file
            var davFolder = new DirectoryInfo(Dav.GetDavDataPath());
            foreach (var folder in davFolder.GetDirectories())
                folder.Delete(true);
            
            // Clear the database
            var database = new davClassLibrary.DataAccess.DavDatabase();
            database.Drop();
        }
        #endregion

        #region Sync
        [Test]
        public async Task SyncShouldDownloadAllTableObjects()
        {
            // Arrange
            ProjectInterface.LocalDataSettings.SetValue(davClassLibrary.Dav.jwtKey, Dav.Jwt);

            // Act
            await davClassLibrary.DataAccess.DataManager.Sync();

            // Assert
            var firstTableObject = davClassLibrary.Dav.Database.GetTableObject(Dav.TestDataFirstTableObject.uuid);
            var secondTableObject = davClassLibrary.Dav.Database.GetTableObject(Dav.TestDataSecondTableObject.uuid);

            Assert.NotNull(firstTableObject);
            Assert.NotNull(secondTableObject);
            Assert.AreEqual(Dav.TestDataFirstTableObject.uuid, firstTableObject.Uuid);
            Assert.AreEqual(Dav.TestDataFirstTableObject.table_id, firstTableObject.TableId);
            Assert.AreEqual(Dav.TestDataFirstTableObject.visibility, davClassLibrary.Models.TableObject.ParseVisibilityToInt(firstTableObject.Visibility));
            Assert.IsFalse(Dav.TestDataFirstTableObject.file);
            Assert.AreEqual(Dav.TestDataFirstTableObject.properties[Dav.TestDataFirstPropertyName], firstTableObject.GetPropertyValue(Dav.TestDataFirstPropertyName));
            Assert.AreEqual(Dav.TestDataFirstTableObject.properties[Dav.TestDataSecondPropertyName], firstTableObject.GetPropertyValue(Dav.TestDataSecondPropertyName));

            Assert.AreEqual(Dav.TestDataSecondTableObject.uuid, secondTableObject.Uuid);
            Assert.AreEqual(Dav.TestDataSecondTableObject.table_id, secondTableObject.TableId);
            Assert.AreEqual(Dav.TestDataSecondTableObject.visibility, davClassLibrary.Models.TableObject.ParseVisibilityToInt(secondTableObject.Visibility));
            Assert.IsFalse(Dav.TestDataSecondTableObject.file);
            Assert.AreEqual(Dav.TestDataSecondTableObject.properties[Dav.TestDataFirstPropertyName], secondTableObject.GetPropertyValue(Dav.TestDataFirstPropertyName));
            Assert.AreEqual(Dav.TestDataSecondTableObject.properties[Dav.TestDataSecondPropertyName], secondTableObject.GetPropertyValue(Dav.TestDataSecondPropertyName));
        }

        [Test]
        public async Task SyncShouldDeleteTableObjectsThatDoNotExistOnTheServer()
        {
            // Arrange
            ProjectInterface.LocalDataSettings.SetValue(davClassLibrary.Dav.jwtKey, Dav.Jwt);
            var uuid = Guid.NewGuid();
            string firstPropertyName = "text";
            string firstPropertyValue = "Lorem ipsum bla bla";
            string secondPropertyName = "test";
            string secondPropertyValue = "true";

            // Create a new table object
            var tableObject = new davClassLibrary.Models.TableObject(uuid, Dav.TestDataTableId);
            var properties = new List<davClassLibrary.Models.Property>
            {
                new davClassLibrary.Models.Property(tableObject.Id, firstPropertyName, firstPropertyValue),
                new davClassLibrary.Models.Property(tableObject.Id, secondPropertyName, secondPropertyValue)
            };

            tableObject.SetUploadStatus(TableObjectUploadStatus.UpToDate);

            // Act
            await davClassLibrary.DataAccess.DataManager.Sync();

            // Assert
            var tableObjectFromDatabase = davClassLibrary.Dav.Database.GetTableObject(tableObject.Uuid);
            Assert.IsNull(tableObjectFromDatabase);

            var firstTableObjectFromServer = davClassLibrary.Dav.Database.GetTableObject(Dav.TestDataFirstTableObject.uuid);
            var secondTableObjectFromServer = davClassLibrary.Dav.Database.GetTableObject(Dav.TestDataSecondTableObject.uuid);

            Assert.IsNotNull(firstTableObjectFromServer);
            Assert.IsNotNull(secondTableObjectFromServer);
        }
        #endregion

        #region SyncPush
        [Test]
        public async Task SyncPushShouldUploadCreatedTableObjects()
        {
            // Arrange
            ProjectInterface.LocalDataSettings.SetValue(davClassLibrary.Dav.jwtKey, Dav.Jwt);
            var uuid = Guid.NewGuid();
            string firstPropertyName = "text";
            string firstPropertyValue = "Lorem ipsum";
            string secondPropertyName = "test";
            string secondPropertyValue = "false";
            // Call this constructor to prevent calling SyncPush() inside it
            var tableObject = new davClassLibrary.Models.TableObject(uuid, Dav.TestDataTableId);
            var properties = new List<davClassLibrary.Models.Property>
            {
                new davClassLibrary.Models.Property(tableObject.Id, firstPropertyName, firstPropertyValue),
                new davClassLibrary.Models.Property(tableObject.Id, secondPropertyName, secondPropertyValue)
            };

            // Act
            await davClassLibrary.DataAccess.DataManager.SyncPush();

            // Assert
            var response = await HttpGet("apps/object/" + tableObject.Uuid);
            var tableObjectFromServer = JsonConvert.DeserializeObject<davClassLibrary.Models.TableObjectData>(response);
            var tableObjectFromDatabase = davClassLibrary.Dav.Database.GetTableObject(tableObject.Uuid);

            // Etags should be equal
            Assert.AreEqual(tableObjectFromServer.etag, tableObjectFromDatabase.Etag);

            // Both table objects should have the same properties
            Assert.AreEqual(tableObject.TableId, tableObjectFromServer.table_id);
            Assert.AreEqual(tableObject.TableId, tableObjectFromDatabase.TableId);
            Assert.AreEqual(davClassLibrary.Models.TableObject.TableObjectUploadStatus.UpToDate, tableObjectFromDatabase.UploadStatus);
            Assert.AreEqual(firstPropertyValue, tableObjectFromDatabase.GetPropertyValue(firstPropertyName));
            Assert.AreEqual(secondPropertyValue, tableObjectFromDatabase.GetPropertyValue(secondPropertyName));
            Assert.AreEqual(firstPropertyValue, tableObjectFromDatabase.GetPropertyValue(firstPropertyName));
            Assert.AreEqual(secondPropertyValue, tableObjectFromDatabase.GetPropertyValue(secondPropertyName));

            // Revert changes
            // Arrange
            tableObject = davClassLibrary.Dav.Database.GetTableObject(uuid);
            tableObject.SetUploadStatus(TableObjectUploadStatus.Deleted);

            // Act
            await davClassLibrary.DataAccess.DataManager.SyncPush();

            // Assert
            var response2 = await HttpGet("apps/object/" + tableObject.Uuid);
            Assert.IsTrue(response2.Contains("2805"));   // Resource does not exist: TableObject
            tableObjectFromDatabase = davClassLibrary.Dav.Database.GetTableObject(tableObject.Uuid);
            Assert.IsNull(tableObjectFromDatabase);
        }

        [Test]
        public async Task SyncPushShouldUploadUpdatedTableObjects()
        {
            // Arrange
            ProjectInterface.LocalDataSettings.SetValue(davClassLibrary.Dav.jwtKey, Dav.Jwt);
            await davClassLibrary.DataAccess.DataManager.Sync();
            var tableObject = davClassLibrary.Dav.Database.GetTableObject(Dav.TestDataFirstTableObject.uuid);
            string firstEtag = tableObject.Etag;
            var property = tableObject.Properties[0];
            string propertyName = property.Name;
            property.Value = "Petropavlovsk-Kamshatski";
            davClassLibrary.Dav.Database.UpdateProperty(property);
            tableObject.SetUploadStatus(TableObjectUploadStatus.Updated);

            // Act
            await davClassLibrary.DataAccess.DataManager.SyncPush();

            // Assert
            var response = await HttpGet("apps/object/" + tableObject.Uuid);
            var tableObjectFromServer = JsonConvert.DeserializeObject<davClassLibrary.Models.TableObjectData>(response);
            var tableObjectFromDatabase = davClassLibrary.Dav.Database.GetTableObject(tableObject.Uuid);

            Assert.AreEqual(tableObjectFromServer.properties[propertyName], property.Value);
            Assert.AreEqual(TableObjectUploadStatus.UpToDate, tableObjectFromDatabase.UploadStatus);
            Assert.AreEqual(tableObjectFromDatabase.GetPropertyValue(propertyName), tableObjectFromServer.properties[propertyName]);

            // Revert changes
            // Arrange
            tableObject = davClassLibrary.Dav.Database.GetTableObject(Dav.TestDataFirstTableObject.uuid);
            property.Value = Dav.TestDataFirstTableObjectFirstPropertyValue;
            davClassLibrary.Dav.Database.UpdateProperty(property);
            tableObject.SetUploadStatus(TableObjectUploadStatus.Updated);

            // Act
            await davClassLibrary.DataAccess.DataManager.SyncPush();

            // Assert
            var response2 = await HttpGet("apps/object/" + tableObject.Uuid);
            var tableObjectFromServer2 = JsonConvert.DeserializeObject<davClassLibrary.Models.TableObjectData>(response2);
            var tableObjectFromDatabase2 = davClassLibrary.Dav.Database.GetTableObject(tableObject.Uuid);
            string secondEtag = tableObjectFromDatabase2.Etag;

            Assert.AreEqual(tableObjectFromServer2.properties[propertyName], property.Value);
            Assert.AreEqual(TableObjectUploadStatus.UpToDate, tableObjectFromDatabase2.UploadStatus);
            Assert.AreEqual(tableObjectFromDatabase2.GetPropertyValue(propertyName), tableObjectFromServer2.properties[propertyName]);

            // Check if the etag is the same as at the beginning
            Assert.AreEqual(firstEtag, secondEtag);
        }

        [Test]
        public async Task SyncPushShouldUploadDeletedTableObjects()
        {
            // Arrange
            ProjectInterface.LocalDataSettings.SetValue(davClassLibrary.Dav.jwtKey, Dav.Jwt);
            var uuid = Guid.NewGuid();
            string firstPropertyName = "text";
            string firstPropertyValue = "Lorem ipsum";
            string secondPropertyName = "test";
            string secondPropertyValue = "false";

            // Create a new table object
            var tableObject = new davClassLibrary.Models.TableObject(uuid, Dav.TestDataTableId);
            var properties = new List<davClassLibrary.Models.Property>
            {
                new davClassLibrary.Models.Property(tableObject.Id, firstPropertyName, firstPropertyValue),
                new davClassLibrary.Models.Property(tableObject.Id, secondPropertyName, secondPropertyValue)
            };

            // Upload the new table object
            await davClassLibrary.DataAccess.DataManager.SyncPush();

            // Check if the table object was uploaded
            var response = await HttpGet("apps/object/" + tableObject.Uuid);
            var tableObjectFromServer = JsonConvert.DeserializeObject<davClassLibrary.Models.TableObjectData>(response);
            Assert.AreEqual(Dav.TestDataTableId, tableObjectFromServer.table_id);
            Assert.AreEqual(firstPropertyValue, tableObjectFromServer.properties[firstPropertyName]);
            Assert.AreEqual(secondPropertyValue, tableObjectFromServer.properties[secondPropertyName]);

            tableObject = davClassLibrary.Dav.Database.GetTableObject(tableObject.Uuid);
            tableObject.SetUploadStatus(TableObjectUploadStatus.Deleted);

            // Act
            await davClassLibrary.DataAccess.DataManager.SyncPush();

            // Assert
            var response2 = await HttpGet("apps/object/" + tableObject.Uuid);
            Assert.IsTrue(response2.Contains("2805"));

            tableObject = davClassLibrary.Dav.Database.GetTableObject(tableObject.Uuid);
            Assert.IsNull(tableObject);
        }

        [Test]
        public async Task SyncPushShouldDeleteUpdatedTableObjectsThatDoNotExistOnTheServer()
        {
            // Arrange
            ProjectInterface.LocalDataSettings.SetValue(davClassLibrary.Dav.jwtKey, Dav.Jwt);
            var uuid = Guid.NewGuid();
            string firstPropertyName = "text";
            string firstPropertyValue = "Lorem ipsum bla bla";
            string secondPropertyName = "test";
            string secondPropertyValue = "true";

            // Create a new table object
            var tableObject = new davClassLibrary.Models.TableObject(uuid, Dav.TestDataTableId);
            var properties = new List<davClassLibrary.Models.Property>
            {
                new davClassLibrary.Models.Property(tableObject.Id, firstPropertyName, firstPropertyValue),
                new davClassLibrary.Models.Property(tableObject.Id, secondPropertyName, secondPropertyValue)
            };

            tableObject.SetUploadStatus(TableObjectUploadStatus.Updated);

            // Act
            await davClassLibrary.DataAccess.DataManager.SyncPush();

            // Assert
            tableObject = davClassLibrary.Dav.Database.GetTableObject(tableObject.Uuid);
            Assert.IsNull(tableObject);
        }

        [Test]
        public async Task SyncPushShouldDeleteDeletedTableObjectsThatDoNotExistOnTheServer()
        {
            // Arrange
            ProjectInterface.LocalDataSettings.SetValue(davClassLibrary.Dav.jwtKey, Dav.Jwt);
            var uuid = Guid.NewGuid();
            string firstPropertyName = "text";
            string firstPropertyValue = "Lorem ipsum bla bla";
            string secondPropertyName = "test";
            string secondPropertyValue = "true";

            // Create a new table object
            var tableObject = new davClassLibrary.Models.TableObject(uuid, Dav.TestDataTableId);
            var properties = new List<davClassLibrary.Models.Property>
            {
                new davClassLibrary.Models.Property(tableObject.Id, firstPropertyName, firstPropertyValue),
                new davClassLibrary.Models.Property(tableObject.Id, secondPropertyName, secondPropertyValue)
            };

            tableObject.SetUploadStatus(TableObjectUploadStatus.Deleted);

            // Act
            await davClassLibrary.DataAccess.DataManager.SyncPush();

            // Assert
            tableObject = davClassLibrary.Dav.Database.GetTableObject(tableObject.Uuid);
            Assert.IsNull(tableObject);
        }
        #endregion

        #region SortTableIds
        [Test]
        public void SortTableIdsShouldReturnTheCorrectArrayWhenThereAreNoParallelTableIds()
        {
            /*
                Input:
                    tableIds:            1, 2, 3, 4
                    parallelTableIds:    
                    pages:               2, 2, 2, 2

                Output:
                    [1, 1, 2, 2, 3, 3, 4, 4]
            */
            // Arrange
            List<int> tableIds = new List<int> { 1, 2, 3, 4 };
            List<int> parallelTableIds = new List<int>();
            Dictionary<int, int> tableIdPages = new Dictionary<int, int>();
            tableIdPages[1] = 2;
            tableIdPages[2] = 2;
            tableIdPages[3] = 2;
            tableIdPages[4] = 2;

            // Act
            List<int> sortedTableIds = davClassLibrary.DataAccess.DataManager.SortTableIds(tableIds, parallelTableIds, tableIdPages);

            // Assert
            Assert.AreEqual(new List<int> { 1, 1, 2, 2, 3, 3, 4, 4 }, sortedTableIds);
        }

        [Test]
        public void SortTableIdsShouldReturnTheCorrectArrayWhenThereIsOneParallelTableId()
        {
            /*
                Input:
                    tableIds:            1, 2, 3, 4
                    parallelTableIds:       2
                    pages:               2, 2, 2, 2

                Output:
                    [1, 1, 2, 2, 3, 3, 4, 4]
            */
            // Arrange
            List<int> tableIds = new List<int> { 1, 2, 3, 4 };
            List<int> parallelTableIds = new List<int> { 2 };
            Dictionary<int, int> tableIdPages = new Dictionary<int, int>();
            tableIdPages[1] = 2;
            tableIdPages[2] = 2;
            tableIdPages[3] = 2;
            tableIdPages[4] = 2;

            // Act
            List<int> sortedTableIds = davClassLibrary.DataAccess.DataManager.SortTableIds(tableIds, parallelTableIds, tableIdPages);

            // Assert
            Assert.AreEqual(new List<int> { 1, 1, 2, 2, 3, 3, 4, 4 }, sortedTableIds);
        }

        [Test]
        public void SortTableIdsShouldReturnTheCorrectArrayWhenTheParallelTableIdsAreSideBySide()
        {
            /*
                Input:
                    tableIds:            1, 2, 3, 4
                    parallelTableIds:       2, 3
                    pages:               2, 2, 2, 2

                Output:
                    [1, 1, 2, 3, 2, 3, 4, 4]
            */
            // Arrange
            List<int> tableIds = new List<int> { 1, 2, 3, 4 };
            List<int> parallelTableIds = new List<int> { 2, 3 };
            Dictionary<int, int> tableIdPages = new Dictionary<int, int>();
            tableIdPages[1] = 2;
            tableIdPages[2] = 2;
            tableIdPages[3] = 2;
            tableIdPages[4] = 2;

            // Act
            List<int> sortedTableIds = davClassLibrary.DataAccess.DataManager.SortTableIds(tableIds, parallelTableIds, tableIdPages);

            // Assert
            Assert.AreEqual(new List<int> { 1, 1, 2, 3, 2, 3, 4, 4 }, sortedTableIds);
        }

        [Test]
        public void SortTableIdsShouldReturnTheCorrectArrayWhenTheParallelTableIdsAreNotSideBySide()
        {
            /*
                Input:
                    tableIds:            1, 2, 3, 4
                    parallelTableIds:    1,       4
                    pages:               2, 2, 2, 2

                Output:
                    [1, 2, 2, 3, 3, 4, 1, 4]
            */
            // Arrange
            List<int> tableIds = new List<int> { 1, 2, 3, 4 };
            List<int> parallelTableIds = new List<int> { 1, 4 };
            Dictionary<int, int> tableIdPages = new Dictionary<int, int>();
            tableIdPages[1] = 2;
            tableIdPages[2] = 2;
            tableIdPages[3] = 2;
            tableIdPages[4] = 2;

            // Act
            List<int> sortedTableIds = davClassLibrary.DataAccess.DataManager.SortTableIds(tableIds, parallelTableIds, tableIdPages);

            // Assert
            Assert.AreEqual(new List<int> { 1, 2, 2, 3, 3, 4, 1, 4 }, sortedTableIds);
        }

        [Test]
        public void SortTableIdsShouldReturnTheCorrectArrayWhenThereAreDifferentPagesAndTheParallelTableIdsAreNotSideBySide()
        {
            /*
                Input:
                    tableIds:            1, 2, 3, 4
                    parallelTableIds:    1,       4
                    pages:               3, 1, 2, 4

                Output:
                    [1, 2, 3, 3, 4, 1, 4, 1, 4, 4]
            */
            // Arrange
            List<int> tableIds = new List<int> { 1, 2, 3, 4 };
            List<int> parallelTableIds = new List<int> { 1, 4 };
            Dictionary<int, int> tableIdPages = new Dictionary<int, int>();
            tableIdPages[1] = 3;
            tableIdPages[2] = 1;
            tableIdPages[3] = 2;
            tableIdPages[4] = 4;

            // Act
            List<int> sortedTableIds = davClassLibrary.DataAccess.DataManager.SortTableIds(tableIds, parallelTableIds, tableIdPages);

            // Assert
            Assert.AreEqual(new List<int> { 1, 2, 3, 3, 4, 1, 4, 1, 4, 4 }, sortedTableIds);
        }

        [Test]
        public void SortTableIdsShouldReturnTheCorrectArrayWhenThereAreDifferentPagesAndTheParallelTableIdsAreSideBySide()
        {
            /*
                Input:
                    tableIds:            1, 2, 3, 4
                    parallelTableIds:    1, 2
                    pages:               2, 4, 3, 2

                Output:
                    [1, 2, 1, 2, 2, 2, 3, 3, 3, 4, 4]
            */
            // Arrange
            List<int> tableIds = new List<int> { 1, 2, 3, 4 };
            List<int> parallelTableIds = new List<int> { 1, 2 };
            Dictionary<int, int> tableIdPages = new Dictionary<int, int>();
            tableIdPages[1] = 2;
            tableIdPages[2] = 4;
            tableIdPages[3] = 3;
            tableIdPages[4] = 2;

            // Act
            List<int> sortedTableIds = davClassLibrary.DataAccess.DataManager.SortTableIds(tableIds, parallelTableIds, tableIdPages);

            // Assert
            Assert.AreEqual(new List<int> { 1, 2, 1, 2, 2, 2, 3, 3, 3, 4, 4 }, sortedTableIds);
        }
        #endregion

        private static async Task<string> HttpGet(string url)
        {
            HttpClient httpClient = new HttpClient();
            var headers = httpClient.DefaultRequestHeaders;
            headers.Authorization = new AuthenticationHeaderValue(Dav.Jwt);
            Uri requestUri = new Uri(davClassLibrary.Dav.ApiBaseUrl + url);

            //Send the GET request
            HttpResponseMessage httpResponse = new HttpResponseMessage();
            httpResponse = await httpClient.GetAsync(requestUri);
            return await httpResponse.Content.ReadAsStringAsync();
        }
    }
}
