﻿using davClassLibrary.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace davClassLibrary.Tests.Models
{
    [TestFixture][SingleThreaded]
    public class TableObjectTest
    {
        #region Setup
        [OneTimeSetUp]
        public void GlobalSetup()
        {
            Utils.GlobalSetup();
        }

        [SetUp]
        public async Task Setup()
        {
            Dav.IsLoggedIn = false;
            Dav.AccessToken = null;

            // Delete all files and folders in the test folder except the database file
            var davFolder = new DirectoryInfo(Utils.GetDavDataPath());
            foreach (var folder in davFolder.GetDirectories())
                folder.Delete(true);

            // Clear the database
            var database = new davClassLibrary.DataAccess.DavDatabase();
            await database.DropAsync();
        }
        #endregion

        #region Create
        [Test]
        public async Task CreateWithTableIdShouldCreateNewTableObject()
        {
            // Arrange
            int tableId = 4;

            // Act
            var tableObject = await TableObject.CreateAsync(tableId);

            // Assert
            Assert.AreEqual(tableId, tableObject.TableId);

            var tableObject2 = await Dav.Database.GetTableObjectAsync(tableObject.Uuid);
            Assert.IsNotNull(tableObject2);
            Assert.AreEqual(tableObject.Id, tableObject2.Id);
            Assert.AreEqual(tableObject.TableId, tableObject2.TableId);
            Assert.AreEqual(tableObject.Uuid, tableObject2.Uuid);
        }

        [Test]
        public async Task CreateWithUuidAndTableIdShouldCreateNewTableObject()
        {
            // Arrange
            int tableId = 4;
            Guid uuid = Guid.NewGuid();

            // Act
            var tableObject = await TableObject.CreateAsync(uuid, tableId);

            // Assert
            Assert.AreEqual(tableId, tableObject.TableId);
            Assert.AreEqual(uuid, tableObject.Uuid);

            var tableObject2 = await Dav.Database.GetTableObjectAsync(uuid);
            Assert.IsNotNull(tableObject2);
            Assert.AreEqual(tableObject.TableId, tableObject2.TableId);
            Assert.AreEqual(tableObject.Id, tableObject2.Id);
            Assert.AreEqual(tableObject.Uuid, tableObject2.Uuid);
        }

        [Test]
        public async Task CreateWithUuidTableIdAndFileShouldCreateNewTableObject()
        {
            // Arrange
            int tableId = 4;
            Guid uuid = Guid.NewGuid();
            FileInfo file = new FileInfo(Path.Combine(Utils.GetProjectDirectory(), "Assets", "image.jpg"));
            string newFilePath = Path.Combine(Utils.GetDavDataPath(), tableId.ToString(), uuid.ToString());

            // Act
            var tableObject = await TableObject.CreateAsync(uuid, tableId, file);

            // Assert
            Assert.AreEqual(tableId, tableObject.TableId);
            Assert.AreEqual(uuid, tableObject.Uuid);
            Assert.IsTrue(tableObject.IsFile);
            Assert.AreEqual(newFilePath, tableObject.File.FullName);

            var tableObject2 = await Dav.Database.GetTableObjectAsync(uuid);
            Assert.IsNotNull(tableObject2);
            Assert.AreEqual(tableObject.TableId, tableObject2.TableId);
            Assert.AreEqual(tableObject.Id, tableObject2.Id);
            Assert.AreEqual(tableObject.Uuid, tableObject2.Uuid);
            Assert.AreEqual(newFilePath, tableObject2.File.FullName);
            Assert.AreEqual(tableObject.Properties.Find(prop => prop.Name == "ext").Value, file.Extension.Replace(".", ""));
        }

        [Test]
        public async Task CreateWithUuidTableIdAndPropertiesShouldCreateNewTableObject()
        {
            // Arrange
            var tableId = 4;
            Guid uuid = Guid.NewGuid();
            List<Property> propertiesList = new List<Property>
            {
                new Property{ Name = "page1", Value = "Hallo Welt" },
                new Property{ Name = "page2", Value = "Hello World" }
            };

            // Act
            var tableObject = await TableObject.CreateAsync(uuid, tableId, propertiesList);

            // Assert
            Assert.AreEqual(tableId, tableObject.TableId);
            Assert.AreEqual(uuid, tableObject.Uuid);
            Assert.AreEqual(propertiesList, tableObject.Properties);

            var tableObject2 = await Dav.Database.GetTableObjectAsync(uuid);
            Assert.IsNotNull(tableObject2);
            Assert.AreEqual(tableObject.TableId, tableObject2.TableId);
            Assert.AreEqual(tableObject.Id, tableObject2.Id);
            Assert.AreEqual(tableObject.Uuid, tableObject2.Uuid);
        }
        #endregion

        #region SetFile
        [Test]
        public async Task SetFileShouldCopyTheFileAndSaveTheExtInTheDatabase()
        {
            // Arrange
            int tableId = 3;
            Guid uuid = Guid.NewGuid();
            FileInfo oldFile = new FileInfo(Path.Combine(Utils.GetProjectDirectory(), "Assets", "image.jpg"));
            FileInfo newFile = new FileInfo(Path.Combine(Utils.GetProjectDirectory(), "Assets", "icon.ico"));

            var tableObject = await TableObject.CreateAsync(uuid, tableId, oldFile);

            // Act
            await tableObject.SetFileAsync(newFile);

            // Assert
            Assert.AreNotEqual(oldFile, tableObject.File);
            Assert.AreEqual(newFile.Length, tableObject.File.Length);

            var tableObject2 = await Dav.Database.GetTableObjectAsync(uuid);
            Assert.NotNull(tableObject2);
            Assert.AreEqual(newFile.Length, tableObject2.File.Length);
            Assert.AreEqual(tableObject2.Properties.Find(prop => prop.Name == "ext").Value, newFile.Extension.Replace(".", ""));
        }

        [Test]
        public async Task SetFileShouldNotWorkWhenTheTableObjectIsNotAFile()
        {
            // Arrange
            int tableId = 3;
            FileInfo file = new FileInfo(Path.Combine(Utils.GetProjectDirectory(), "Assets", "image.jpg"));
            var tableObject = await TableObject.CreateAsync(tableId);

            // Act
            await tableObject.SetFileAsync(file);

            // Assert
            var tableObject2 = await Dav.Database.GetTableObjectAsync(tableObject.Uuid);
            Assert.IsFalse(tableObject2.IsFile);
            Assert.IsNull(tableObject2.File);
        }
        #endregion

        #region SetPropertyValue
        [Test]
        public async Task SetPropertyValueShouldCreateANewPropertyAndSaveItInTheDatabase()
        {
            // Arrange
            int tableId = 4;
            string propertyName = "page1";
            string propertyValue = "Hello World";
            var tableObject = await TableObject.CreateAsync(tableId);

            // Act
            await tableObject.SetPropertyValueAsync(propertyName, propertyValue);

            // Assert
            Assert.AreEqual(tableObject.Properties.Count, 1);
            Assert.AreEqual(tableObject.Properties[0].Name, propertyName);
            Assert.AreEqual(tableObject.Properties[0].Value, propertyValue);

            var tableObject2 = await Dav.Database.GetTableObjectAsync(tableObject.Uuid);
            Assert.NotNull(tableObject2);
            Assert.AreEqual(tableObject2.Properties.Count, 1);
            Assert.AreEqual(tableObject2.Properties[0].Name, propertyName);
            Assert.AreEqual(tableObject2.Properties[0].Value, propertyValue);
        }

        [Test]
        public async Task SetPropertyValueShouldUpdateAnExistingPropertyAndSaveItInTheDatabase()
        {
            // Arrange
            int tableId = 4;
            Guid uuid = Guid.NewGuid();
            string propertyName = "page1";
            string oldPropertyValue = "Hello World";
            string newPropertyValue = "Hallo Welt";
            List<Property> propertiesList = new List<Property>
            {
                new Property{ Name = propertyName, Value = oldPropertyValue }
            };
            var tableObject = await TableObject.CreateAsync(uuid, tableId, propertiesList);

            Assert.AreEqual(tableObject.Properties.Count, 1);
            Assert.AreEqual(tableObject.Properties[0].Name, propertyName);
            Assert.AreEqual(tableObject.Properties[0].Value, oldPropertyValue);

            // Act
            await tableObject.SetPropertyValueAsync(propertyName, newPropertyValue);

            // Assert
            Assert.AreEqual(tableObject.Properties.Count, 1);
            Assert.AreEqual(tableObject.Properties[0].Name, propertyName);
            Assert.AreEqual(tableObject.Properties[0].Value, newPropertyValue);

            var tableObject2 = await Dav.Database.GetTableObjectAsync(uuid);
            Assert.AreEqual(tableObject2.Properties.Count, 1);
            Assert.AreEqual(tableObject2.Properties[0].Name, propertyName);
            Assert.AreEqual(tableObject2.Properties[0].Value, newPropertyValue);
        }
        #endregion

        #region SetPropertyValues
        [Test]
        public async Task SetPropertyValuesShouldCreateNewPropertiesAndSaveThemInTheDatabase()
        {
            // Arrange
            Guid uuid = Guid.NewGuid();
            var tableId = 4;
            var firstPropertyName = "page1";
            var secondPropertyName = "page2";
            var firstPropertyValue = "test";
            var secondPropertyValue = "blablabla";

            var tableObject = await TableObject.CreateAsync(uuid, tableId);

            Dictionary<string, string> newProperties = new Dictionary<string, string>
            {
                { firstPropertyName, firstPropertyValue },
                { secondPropertyName, secondPropertyValue }
            };

            // Act
            await tableObject.SetPropertyValuesAsync(newProperties);

            // Assert
            Assert.AreEqual(firstPropertyValue, tableObject.GetPropertyValue(firstPropertyName));
            Assert.AreEqual(secondPropertyValue, tableObject.GetPropertyValue(secondPropertyName));

            // Make sure the properties have ids
            Assert.AreNotEqual(0, tableObject.Properties[0].Id);
            Assert.AreNotEqual(0, tableObject.Properties[1].Id);

            var tableObjectFromDatabase = await Dav.Database.GetTableObjectAsync(uuid);
            Assert.IsNotNull(tableObjectFromDatabase);
            Assert.AreEqual(2, tableObjectFromDatabase.Properties.Count);
            Assert.AreEqual(firstPropertyName, tableObjectFromDatabase.Properties[0].Name);
            Assert.AreEqual(firstPropertyValue, tableObjectFromDatabase.Properties[0].Value);
            Assert.AreEqual(secondPropertyName, tableObjectFromDatabase.Properties[1].Name);
            Assert.AreEqual(secondPropertyValue, tableObjectFromDatabase.Properties[1].Value);
        }

        [Test]
        public async Task SetPropertyValuesShouldUpdateExistingPropertiesAndSaveThemInTheDatabase()
        {
            // Arrange
            Guid uuid = Guid.NewGuid();
            var tableId = 4;
            var firstPropertyName = "page1";
            var oldFirstPropertyValue = "test";
            var newFirstPropertyValue = "testtest";
            var secondPropertyName = "page2";
            var oldSecondPropertyValue = "blabla";
            var newSecondPropertyValue = "blablub";
            List<Property> properties = new List<Property>
            {
                new Property { Name = firstPropertyName, Value = oldFirstPropertyValue },
                new Property { Name = secondPropertyName, Value = oldSecondPropertyValue }
            };

            var tableObject = await TableObject.CreateAsync(uuid, tableId, properties);

            Dictionary<string, string> newProperties = new Dictionary<string, string>
            {
                { firstPropertyName, newFirstPropertyValue },
                { secondPropertyName, newSecondPropertyValue }
            };

            // Act
            await tableObject.SetPropertyValuesAsync(newProperties);

            // Assert
            Assert.AreEqual(newFirstPropertyValue, tableObject.GetPropertyValue(firstPropertyName));
            Assert.AreEqual(newSecondPropertyValue, tableObject.GetPropertyValue(secondPropertyName));

            var tableObjectFromDatabase = await Dav.Database.GetTableObjectAsync(uuid);
            Assert.IsNotNull(tableObjectFromDatabase);
            Assert.AreEqual(2, tableObjectFromDatabase.Properties.Count);
            Assert.AreEqual(firstPropertyName, tableObjectFromDatabase.Properties[0].Name);
            Assert.AreEqual(newFirstPropertyValue, tableObjectFromDatabase.Properties[0].Value);
            Assert.AreEqual(secondPropertyName, tableObjectFromDatabase.Properties[1].Name);
            Assert.AreEqual(newSecondPropertyValue, tableObjectFromDatabase.Properties[1].Value);
        }
        #endregion

        #region GetPropertyValue
        [Test]
        public async Task GetPropertyValueShouldReturnTheValueOfTheProperty()
        {
            // Arrange
            int tableId = 4;
            Guid uuid = Guid.NewGuid();
            string propertyName = "page1";
            string propertyValue = "Hello World";
            List<Property> propertiesList = new List<Property>
            {
                new Property{ Name = propertyName, Value = propertyValue }
            };
            var tableObject = await TableObject.CreateAsync(uuid, tableId, propertiesList);

            Assert.AreEqual(tableObject.Properties.Count, 1);
            Assert.AreEqual(tableObject.Properties[0].Name, propertyName);
            Assert.AreEqual(tableObject.Properties[0].Value, propertyValue);

            // Act
            string newPropertyValue = tableObject.GetPropertyValue(propertyName);

            // Assert
            Assert.AreEqual(propertyValue, newPropertyValue);
        }

        [Test]
        public async Task GetPropertyValueShouldReturnNullIfThePropertyDoesNotExist()
        {
            // Arrange
            int tableId = 4;
            string propertyName = "page1";
            var tableObject = await TableObject.CreateAsync(tableId);

            // Act
            var value = tableObject.GetPropertyValue(propertyName);

            Assert.IsNull(value);
        }
        #endregion

        #region RemoveProperty
        [Test]
        public async Task RemovePropertyShouldRemoveThePropertyFromThePropertiesListAndDeleteItFromTheDatabase()
        {
            // Arrange
            int tableId = 4;
            Guid uuid = Guid.NewGuid();
            string propertyName = "page1";
            string propertyValue = "Hello World";
            List<Property> propertiesList = new List<Property>
            {
                new Property{ Name = propertyName, Value = propertyValue }
            };
            var tableObject = await TableObject.CreateAsync(uuid, tableId, propertiesList);

            // Act
            await tableObject.RemovePropertyAsync(propertyName);

            // Assert
            Assert.AreEqual(0, tableObject.Properties.Count);
            Assert.IsNull(tableObject.GetPropertyValue(propertyName));

            var tableObject2 = await Dav.Database.GetTableObjectAsync(tableObject.Uuid);
            Assert.NotNull(tableObject2);
            Assert.AreEqual(0, tableObject2.Properties.Count);
            Assert.IsNull(tableObject2.GetPropertyValue(propertyName));
        }
        #endregion

        #region SetUploadStatus
        [Test]
        public async Task SetUploadStatusShouldUpdateTheUploadStatusOfTheTableObjectAndSaveItInTheDatabase()
        {
            // Arrange
            int tableId = 4;
            var tableObject = await TableObject.CreateAsync(tableId);
            TableObjectUploadStatus newUploadStatus = TableObjectUploadStatus.Updated;

            Assert.AreEqual(TableObjectUploadStatus.New, tableObject.UploadStatus);

            // Act
            await tableObject.SetUploadStatusAsync(newUploadStatus);

            // Assert
            Assert.AreEqual(newUploadStatus, tableObject.UploadStatus);

            var tableObject2 = await Dav.Database.GetTableObjectAsync(tableObject.Uuid);
            Assert.IsNotNull(tableObject);
            Assert.AreEqual(newUploadStatus, tableObject2.UploadStatus);
        }
        #endregion

        #region Delete
        [Test]
        public async Task DeleteShouldSetTheUploadStatusOfTheTableObjectToDeletedWhenTheUserIsLoggedIn()
        {
            // Arrange
            davClassLibrary.Dav.IsLoggedIn = true;
            davClassLibrary.Dav.AccessToken = Constants.testerXTestAppAccessToken;

            int tableId = 4;
            Guid uuid = Guid.NewGuid();
            List<Property> propertiesList = new List<Property>
            {
                new Property{Name = "page1", Value = "Hello World"},
                new Property{Name = "page2", Value = "Hallo Welt"}
            };
            var tableObject = await TableObject.CreateAsync(uuid, tableId, propertiesList);

            // Act
            await tableObject.DeleteAsync();

            // Assert
            var tableObject2 = await Dav.Database.GetTableObjectAsync(uuid);
            Assert.IsNotNull(tableObject2);
            Assert.AreEqual(TableObjectUploadStatus.Deleted, tableObject2.UploadStatus);
        }

        [Test]
        public async Task DeleteShouldDeleteTheTableObjectImmediatelyWhenTheUserIsNotLoggedIn()
        {
            // Arrange
            int tableId = 4;
            Guid uuid = Guid.NewGuid();
            List<Property> propertiesList = new List<Property>
            {
                new Property{Name = "page1", Value = "Hello World"},
                new Property{Name = "page2", Value = "Hallo Welt"}
            };
            var tableObject = await TableObject.CreateAsync(uuid, tableId, propertiesList);

            int firstPropertyId = tableObject.Properties[0].Id;
            int secondPropertyId = tableObject.Properties[1].Id;

            // Act
            await tableObject.DeleteAsync();

            // Assert
            var tableObjectFromDatabase = await Dav.Database.GetTableObjectAsync(uuid);
            Assert.IsNull(tableObjectFromDatabase);

            var firstPropertyFromDatabase = await Dav.Database.GetPropertyAsync(firstPropertyId);
            Assert.IsNull(firstPropertyFromDatabase);

            var secondPropertyFromDatabase = await Dav.Database.GetPropertyAsync(secondPropertyId);
            Assert.IsNull(secondPropertyFromDatabase);
        }

        [Test]
        public async Task DeleteShouldDeleteTheFileOfTheTableObjectAndDeleteTheTableObjectImmediatelyWhenTheUserIsNotLoggedIn()
        {
            // Arrange
            int tableId = 4;
            Guid uuid = Guid.NewGuid();
            FileInfo file = new FileInfo(Path.Combine(Utils.GetProjectDirectory(), "Assets", "icon.ico"));
            var tableObject = await TableObject.CreateAsync(uuid, tableId, file);
            string filePath = tableObject.File.FullName;

            // Act
            await tableObject.DeleteAsync();

            // Assert
            Assert.IsFalse(File.Exists(filePath));
            var tableObjectFromDatabase = await Dav.Database.GetTableObjectAsync(uuid);
            Assert.IsNull(tableObjectFromDatabase);
        }
        #endregion

        #region DeleteImmediately
        [Test]
        public async Task DeleteImmediatelyShouldDeleteTheTableObjectImmediately()
        {
            // Arrange
            int tableId = 3;
            Guid uuid = Guid.NewGuid();
            List<Property> propertiesList = new List<Property>
            {
                new Property{Name = "page1", Value = "Hello World"},
                new Property{Name = "page2", Value = "Hallo Welt"}
            };
            var tableObject = await TableObject.CreateAsync(uuid, tableId, propertiesList);

            int firstPropertyId = tableObject.Properties[0].Id;
            int secondPropertyId = tableObject.Properties[1].Id;

            // Act
            await tableObject.DeleteImmediatelyAsync();

            // Assert
            var tableObjectFromDatabase = await Dav.Database.GetTableObjectAsync(uuid);
            Assert.IsNull(tableObjectFromDatabase);

            var firstPropertyFromDatabase = await Dav.Database.GetPropertyAsync(firstPropertyId);
            Assert.IsNull(firstPropertyFromDatabase);

            var secondPropertyFromDatabase = await Dav.Database.GetPropertyAsync(secondPropertyId);
            Assert.IsNull(secondPropertyFromDatabase);
        }

        [Test]
        public async Task DeleteImmediatelyShouldDeleteTheTableObjectAndItsFile()
        {
            // Arrange
            int tableId = 4;
            Guid uuid = Guid.NewGuid();
            FileInfo file = new FileInfo(Path.Combine(Utils.GetProjectDirectory(), "Assets", "icon.ico"));
            var tableObject = await TableObject.CreateAsync(uuid, tableId, file);
            string filePath = tableObject.File.FullName;

            // Act
            await tableObject.DeleteImmediatelyAsync();

            // Assert
            var tableObjectFromDatabase = await Dav.Database.GetTableObjectAsync(uuid);
            Assert.IsFalse(File.Exists(filePath));
            Assert.IsNull(tableObjectFromDatabase);
        }
        #endregion

        #region ToTableObjectData
        [Test]
        public void ToTableObjectDataShouldReturnValidTableObjectDataObject()
        {
            // Arrange
            int tableId = 4;
            Guid uuid = Guid.NewGuid();
            string firstPropertyName = "page1";
            string firstPropertyValue = "Hello World";
            string secondPropertyName = "page2";
            string secondPropertyValue = "Hallo Welt";
            List<Property> propertiesList = new List<Property>
            {
                new Property{Name = firstPropertyName, Value = firstPropertyValue},
                new Property{Name = secondPropertyName, Value = secondPropertyValue}
            };
            var tableObject = new TableObject(uuid, tableId, propertiesList);

            // Act
            var tableObjectData = tableObject.ToTableObjectData();

            // Assert
            Assert.AreEqual(tableObject.TableId, tableObjectData.table_id);
            Assert.AreEqual(tableObject.Uuid, tableObjectData.uuid);
            Assert.AreEqual(tableObject.IsFile, tableObjectData.file);
            Assert.AreEqual(firstPropertyValue, tableObjectData.properties[firstPropertyName]);
            Assert.AreEqual(secondPropertyValue, tableObjectData.properties[secondPropertyName]);
        }
        #endregion
    }
}
