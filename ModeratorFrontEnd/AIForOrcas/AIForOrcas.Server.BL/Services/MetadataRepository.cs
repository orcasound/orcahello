using AIForOrcas.Server.BL.Context;
using AIForOrcas.Server.BL.Models.CosmosDB;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace AIForOrcas.Server.BL.Services
{
    public class MetadataRepository
    {
        private readonly ApplicationDbContext _db;

        public MetadataRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public IQueryable<Metadata> GetAll()
        {
            return _db.Metadata.AsQueryable();
        }

        public async Task<Metadata> GetByIdAsync(string id)
        {
            return await _db.Metadata.FirstOrDefaultAsync(x => x.id == id);
        }

        public async Task CommitAsync()
        {
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new DataException(ex.Message);
            }
        }

        public IQueryable<string> GetAllTags()
        {
            return _db.Metadata
                .Where(x => x.tags != null && x.tags != "")
                .Select(x => x.tags)
                .Distinct();
        }

        public IQueryable<Metadata> GetAllWithTag(string tag)
        {
            return _db.Metadata.AsEnumerable()
                .Where(x => x.tags != null && x.tags.Contains(tag))
                .AsQueryable();

        }
    }
}
