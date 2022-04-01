using Signaler.Models;
using System.Collections.Concurrent;

namespace Signaler
{
    /// <summary>
    ///     Manager que faz gestão das salas criadas
    /// </summary>
    public class RoomManager : IRoomManager
    {
        private ConcurrentDictionary<string, Room> _rooms;

        /// <summary>
        ///     ctor
        /// </summary>
        public RoomManager()
        {
            _rooms ??= new ConcurrentDictionary<string, Room>();
        }

        /// <summary>
        ///     Cria uma nova sala
        /// </summary>
        public Room Create(string name)
        {
            var room = new Room()
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
            };

            return _rooms.TryAdd(room.Id, room) ? room : null;
        }

        /// <summary>
        ///     Deleta uma sala
        /// </summary>
        public bool Delete(string id)
        {
            return _rooms.TryRemove(id, out _);
        }

        /// <summary>
        ///     Retorna todas as salas criadas
        /// </summary>
        public IEnumerable<Room> GetAll()
        {
            return _rooms.Values;
        }
    }
}
