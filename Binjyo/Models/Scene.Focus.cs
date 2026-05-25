using System;
using System.Collections.Generic;
using System.Linq;


namespace Binjyo
{
    public partial class Scene
    {
        private static long focusOrderAll = 0;

        public static void Focus(Guid id)
        {
            if (!Items.ContainsKey(id)) return;
            Items[id].focusOrder = ++focusOrderAll;
            Items[id].views.ForEach(view => view.NotifiedFocus());
        }

        private static Guid GetTopFocusId(List<Guid> ids)
        {
            return ids.OrderByDescending(id => Items[id].focusOrder).FirstOrDefault();
        }

        public static void FocusNext()
        {
            List<Guid> allIds = Items.Keys.ToList();
            List<Guid> candidates = GetIdsAtMouse();
            if (candidates.Count == 0)
                candidates = allIds;

            if (candidates.Count == 0)
                return;

            if (candidates.Count == 1)
            {
                Focus(candidates[0]);
                return;
            }

            var topCandidate = GetTopFocusId(candidates);
            int currentIndex = candidates.IndexOf(topCandidate);
            var targetId = candidates[(currentIndex + 1) % candidates.Count];
            Focus(targetId);
        }
    }
}
