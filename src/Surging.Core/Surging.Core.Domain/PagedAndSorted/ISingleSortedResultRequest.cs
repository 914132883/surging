namespace Surging.Core.Domain.PagedAndSorted
{
    public interface ISingleSortedResultRequest
    {
        string Sorting { get; set; }

        SortType SortType { get; set; }
    }
}
