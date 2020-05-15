using System.Collections.Generic;

namespace Surging.Core.Domain.PagedAndSorted
{
    public abstract class PagedAndSortedResultRequestDto : PagedResultRequestDto, IPagedAndSortedResultRequest
    {
        protected PagedAndSortedResultRequestDto() 
        {
            Sorting = new Dictionary<string, SortType>();
        }

        public IDictionary<string, SortType> Sorting { get; set; }
    }
}
