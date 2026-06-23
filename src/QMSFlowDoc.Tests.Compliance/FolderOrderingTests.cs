using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Infrastructure.Persistence;
using QMSFlowDoc.Infrastructure.Services.Folders;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace QMSFlowDoc.Tests.Compliance
{
    public class FolderOrderingTests : IDisposable
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
        private readonly DbContextOptions<QmsDbContext> _options;

        public FolderOrderingTests()
        {
            _connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<QmsDbContext>()
                .UseSqlite(_connection)
                .Options;

            using (var context = new QmsDbContext(_options))
            {
                context.Database.EnsureCreated();
            }
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
        }

        [Fact]
        public async Task CreateFolder_AssignsAutoIncrementedDisplayOrderForSiblings()
        {
            // Arrange
            using var context = new QmsDbContext(_options);
            var service = new FolderService(context);

            // Act: Create root level folders
            await service.CreateFolderAsync("Folder A", null);
            await service.CreateFolderAsync("Folder B", null);

            // Create child level folders
            var parentId = context.Folders.First(f => f.Name == "Folder A").Id;
            await service.CreateFolderAsync("Child 1", parentId);
            await service.CreateFolderAsync("Child 2", parentId);

            // Assert
            var rootFolders = await service.GetFoldersAsync(null);
            var childFolders = await service.GetFoldersAsync(parentId);

            var rootA = rootFolders.First(f => f.Name == "Folder A");
            var rootB = rootFolders.First(f => f.Name == "Folder B");
            var child1 = childFolders.First(f => f.Name == "Child 1");
            var child2 = childFolders.First(f => f.Name == "Child 2");

            Assert.Equal(1, rootA.DisplayOrder);
            Assert.Equal(2, rootB.DisplayOrder);
            Assert.Equal(1, child1.DisplayOrder);
            Assert.Equal(2, child2.DisplayOrder);
        }

        [Fact]
        public async Task MoveFolderUp_SwapsDisplayOrderWithPreviousSibling()
        {
            // Arrange
            using var context = new QmsDbContext(_options);
            var service = new FolderService(context);

            await service.CreateFolderAsync("Folder A", null); // DisplayOrder = 1
            await service.CreateFolderAsync("Folder B", null); // DisplayOrder = 2
            await service.CreateFolderAsync("Folder C", null); // DisplayOrder = 3

            var folderBId = context.Folders.First(f => f.Name == "Folder B").Id;

            // Act
            var success = await service.MoveFolderUpAsync(folderBId);

            // Assert
            Assert.True(success);
            var folders = (await service.GetAllFoldersAsync()).OrderBy(f => f.DisplayOrder).ToList();
            Assert.Equal("Folder B", folders[0].Name);
            Assert.Equal("Folder A", folders[1].Name);
            Assert.Equal("Folder C", folders[2].Name);
        }

        [Fact]
        public async Task MoveFolderDown_SwapsDisplayOrderWithNextSibling()
        {
            // Arrange
            using var context = new QmsDbContext(_options);
            var service = new FolderService(context);

            await service.CreateFolderAsync("Folder A", null); // DisplayOrder = 1
            await service.CreateFolderAsync("Folder B", null); // DisplayOrder = 2
            await service.CreateFolderAsync("Folder C", null); // DisplayOrder = 3

            var folderBId = context.Folders.First(f => f.Name == "Folder B").Id;

            // Act
            var success = await service.MoveFolderDownAsync(folderBId);

            // Assert
            Assert.True(success);
            var folders = (await service.GetAllFoldersAsync()).OrderBy(f => f.DisplayOrder).ToList();
            Assert.Equal("Folder A", folders[0].Name);
            Assert.Equal("Folder C", folders[1].Name);
            Assert.Equal("Folder B", folders[2].Name);
        }

        [Fact]
        public async Task MoveFolder_NormalizesOrders_IfOrdersAreSame()
        {
            // Arrange
            using var context = new QmsDbContext(_options);
            var folderA = new Folder { Id = Guid.NewGuid(), Name = "Folder A", DisplayOrder = 0 };
            var folderB = new Folder { Id = Guid.NewGuid(), Name = "Folder B", DisplayOrder = 0 };
            context.Folders.AddRange(folderA, folderB);
            context.SaveChanges();

            var service = new FolderService(context);

            // Act: Folder B should go Up (before A)
            var success = await service.MoveFolderUpAsync(folderB.Id);

            // Assert
            Assert.True(success);
            var folders = (await service.GetAllFoldersAsync()).OrderBy(f => f.DisplayOrder).ToList();
            Assert.Equal("Folder B", folders[0].Name);
            Assert.Equal("Folder A", folders[1].Name);
        }
    }
}
