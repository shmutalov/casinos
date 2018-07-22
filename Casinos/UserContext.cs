using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Casinos;
using Microsoft.EntityFrameworkCore.Storage;
using MySql.Data.EntityFrameworkCore.Extensions;

namespace Casinos
{
    public class UserContext : DbContext
    {
        public DbSet<User> users { get; set; }

        public UserContext()
        {
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySQL("Server=127.0.0.1;Database=kazino;Uid=root;SslMode=none;");
        }
    }
}