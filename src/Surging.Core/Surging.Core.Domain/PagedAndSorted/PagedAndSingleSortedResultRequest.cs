namespace Surging.Core.Domain.PagedAndSorted
{
    public abstract class PagedAndSingleSortedResultRequest : PagedResultRequestDto, IPagedAndSingleSortedResultRequest
    {
        public string Sorting { get; set; }
        public SortType SortType { get; set; } = SortType.Asc;
    }
}
