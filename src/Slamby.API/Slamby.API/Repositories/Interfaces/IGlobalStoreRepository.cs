namespace Slamby.API
{
    public interface IGlobalStoreRepository<T>
    {
        void Add(string name, T item);
        T Get(string id);
        bool IsExist(string id);
        void Remove(string id);
    }
}