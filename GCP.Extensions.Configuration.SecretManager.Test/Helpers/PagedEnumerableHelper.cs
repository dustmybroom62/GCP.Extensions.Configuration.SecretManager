using System;
using System.Collections.Generic;
using System.Text;
using Google.Api.Gax;

namespace GCP.Extensions.Configuration.SecretManager.Test.Helpers
{
    class PagedEnumerableHelper<TResponse, TResource> : PagedEnumerable<TResponse, TResource>
    {
        private IEnumerable<TResource> _resources;

        public PagedEnumerableHelper(IEnumerable<TResource> resources)
        {
            _resources = resources;
        }

        public override IEnumerator<TResource> GetEnumerator()
        {
            return _resources.GetEnumerator();
        }
    }
}
