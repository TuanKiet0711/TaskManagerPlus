using TaskManagerPlus.Services;

namespace TaskManagerPlus.Models
{
    public class GroupHeader
    {
        public string Key { get; }
        public int Count { get; }
        public string DisplayText => $"{LocalizationService.T(Key)} ({Count})";

        public GroupHeader(string key, int count)
        {
            Key = key;
            Count = count;
        }
    }
}
