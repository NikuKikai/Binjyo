using System;
using System.Collections.Generic;
using System.Linq;


namespace Binjyo
{
    public partial class Scene
    {
        private static long focusOrderAll = 0;
        public static Guid FocusedId { get; private set; } = Guid.Empty;

        public static void Focus(Guid id)
        {
            var formerId = FocusedId;
            if (id == formerId) return;
            if (!Items.ContainsKey(id)) return;

            Items[id].FocusOrder = ++focusOrderAll;
            FocusedId = id;
            Items[id].views.ForEach(view => view.NotifiedFocus());

            if (!Items.ContainsKey(formerId)) return;
            Items[formerId].views.ForEach(view => view.NotifiedFocus());
        }

        private static Guid GetTopFocusId(List<Guid> ids)
        {
            return ids.OrderByDescending(id => Items[id].FocusOrder).FirstOrDefault();
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
