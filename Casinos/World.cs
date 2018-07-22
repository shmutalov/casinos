using MySql.Data.MySqlClient;
using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Casinos
{
    class World
    {
        public int CheckBalance(long? userid)
        {
            using (UserContext db = new UserContext())
            {
                return db.users.Find((int)userid).balance;
            }
        }
        public bool IfRegistered(long? userid)
        {
            using (UserContext db = new UserContext())
            {
                return db.users.Where(u => u.userid == (int)userid).Take(1).Any();
            }
        }

        public void Register(long? userid)
        {
            using (UserContext db = new UserContext())
            {
                User user = new User { userid = (int)userid, balance = 1000 };
                db.users.Add(user);
                db.SaveChanges();
            }
        }
        public void GameFirst(long? userid, int value)
        {
            using (UserContext db = new UserContext())
            {
                var customer = db.users
                    .Where(c => c.userid == (int)userid)
                    .FirstOrDefault();

                // Внести изменения
                customer.balance = value;
                db.users.Where(u => u.userid == (int)userid).Take(1);
                db.SaveChanges();
            }
        }
    }
}
