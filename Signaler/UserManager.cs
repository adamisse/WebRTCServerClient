using Signaler.Models;
using System.Collections.Concurrent;

namespace Signaler
{
    /// <summary>
    ///     Manager de usuarios do sistema
    /// </summary>
    public class UserManager : IUserManager
    {
        private ConcurrentDictionary<string, User> _users;

        /// <summary>
        ///     ctor
        /// </summary>
        public UserManager()
        {
            _users ??= new ConcurrentDictionary<string, User>();
        }

        /// <summary>
        ///     Cria um novo usuario
        /// </summary>
        public User Create(string connectionId, string username)
        {
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                ConnectionId = connectionId,
                Username = username
            };

            return _users.TryAdd(user.Id, user) ? user : null;
        }

        /// <summary>
        ///     Deleta um usuário
        /// </summary>
        public bool Delete(string id)
        {
            return _users.TryRemove(id, out _);
        }

        /// <summary>
        ///     Retorna todos os usuarios
        /// </summary>
        public IEnumerable<User> GetAll()
        {
            return _users.Values;
        }
    }
}
