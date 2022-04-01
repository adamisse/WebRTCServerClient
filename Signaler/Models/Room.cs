namespace Signaler.Models
{
    /// <summary>
    ///     Sala de chamada
    /// </summary>
    public class Room
    {
        /// <summary>
        ///     ctor
        /// </summary>
        public Room()
        {
            Users ??= new List<User>();
        }

        /// <summary>
        ///     Id da sala (gerado automaticamente)
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        ///     Nome da sala
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Lista de usuarios
        /// </summary>
        public IList<User> Users { get; set; }

        /// <summary>
        ///     Adiciona um usuário a sala em questão
        /// </summary>
        public void AddUser(User user)
        {
            Users.Add(user);
        }

        /// <summary>
        ///     Remove um usuário a sala em questão
        /// </summary>
        public void RemoveUser(User user)
        {
            Users.Remove(user);
        }
    }
}
