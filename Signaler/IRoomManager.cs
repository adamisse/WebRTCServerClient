using Signaler.Models;

namespace Signaler
{
    public interface IRoomManager
    {
        Room Create(string name);
        bool Delete(string id);
        IEnumerable<Room> GetAll();
    }
}