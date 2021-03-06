﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using SmartStore.Collections;
using SmartStore.Core.Domain.Cms;
using SmartStore.Core.Localization;
using SmartStore.Services.Localization;

namespace SmartStore.Web.Framework.UI
{
    public static class MenuExtensions
	{
		public static IEnumerable<TreeNode<MenuItem>> GetBreadcrumb(this TreeNode<MenuItem> node)
		{
			Guard.NotNull(node, nameof(node));

			return node.Trail.Where(x => !x.IsRoot);
		}

		public static string GetItemText(this TreeNode<MenuItem> node, Localizer localizer)
        {
            string result = null;

            if (node.Value.ResKey.HasValue())
            {
                result = localizer(node.Value.ResKey).Text;
            }

            if (!result.HasValue() || result.IsCaseInsensitiveEqual(node.Value.ResKey))
            {
                result = node.Value.Text;
            }

            return result;
        }

		/// <summary>
		/// Gets the state of <c>node</c> within the passed <c>currentPath</c>, which is the navigation breadcrumb.
		/// </summary>
		/// <param name="node">The node to get the state for</param>
		/// <param name="currentPath">The current path/breadcrumb</param>
		/// <returns>
		///		<see cref="NodePathState" /> enumeration indicating whether the node is in the current path (<c>Selected</c> or <c>Expanded</c>)
		///		and whether it has children (<c>Parent</c>)
		///	</returns>
		public static NodePathState GetNodePathState(this TreeNode<MenuItem> node, IEnumerable<TreeNode<MenuItem>> currentPath)
		{
			return GetNodePathState(node, currentPath.Select(x => x.Value).ToList());
		}

		/// <summary>
		/// Gets the state of <c>node</c> within the passed <c>currentPath</c>, which is the navigation breadcrumb.
		/// </summary>
		/// <param name="node">The node to get the state for</param>
		/// <param name="currentPath">The current path/breadcrumb</param>
		/// <returns>
		///		<see cref="NodePathState" /> enumeration indicating whether the node is in the current path (<c>Selected</c> or <c>Expanded</c>)
		///		and whether it has children (<c>Parent</c>)
		///	</returns>
		public static NodePathState GetNodePathState(this TreeNode<MenuItem> node, IList<MenuItem> currentPath)
		{
			Guard.NotNull(currentPath, nameof(currentPath));
			
			var state = NodePathState.Unknown;

			if (node.HasChildren)
			{
				state = state | NodePathState.Parent;
			}

			var lastInPath = currentPath.LastOrDefault();

			if (currentPath.Count > 0)
			{
				if (node.Value.Equals(lastInPath))
				{
					state = state | NodePathState.Selected;
				}
				else
				{
					if (node.Depth - 1 < currentPath.Count)
					{
						if (currentPath[node.Depth - 1].Equals(node.Value))
						{
							state = state | NodePathState.Expanded;
						}
					}
				}
			}

			return state;
		}

        /// <summary>
        /// Creates a menu model.
        /// </summary>
        /// <param name="menu">Menu.</param>
        /// <param name="context">Controller context to resolve current node. Can be <c>null</c>.</param>
        /// <returns>Menu model.</returns>
        public static MenuModel CreateModel(this IMenu menu, ControllerContext context)
        {
            Guard.NotNull(menu, nameof(menu));

            var model = new MenuModel
            {
                Name = menu.Name,
                Root = menu.Root
            };

            var currentNode = menu.ResolveCurrentNode(context);

            model.Path = currentNode != null
                ? currentNode.Trail.Where(x => !x.IsRoot).ToList()
                : new List<TreeNode<MenuItem>>();

            menu.ResolveElementCounts(model.SelectedNode, false);

            return model;
        }

        /// <summary>
        /// Converts a list of menu items into a tree.
        /// </summary>
        /// <param name="origin">Origin of the tree.</param>
        /// <param name="items">List of menu items.</param>
        /// <param name="itemProviders">Menu item providers.</param>
        /// <param name="includeItemsWithoutExistingParent">Whether to include menu items without existing parent menu item.</param>
        /// <returns>Tree of menu items.</returns>
        public static TreeNode<MenuItem> GetTree(
            this IEnumerable<MenuItemRecord> items,
            string origin,
            IDictionary<string, Lazy<IMenuItemProvider, MenuItemProviderMetadata>> itemProviders,
            bool includeItemsWithoutExistingParent = false)
        {
            Guard.NotNull(items, nameof(items));
            Guard.NotNull(itemProviders, nameof(itemProviders));

            if (!items.Any())
            {
                return new TreeNode<MenuItem>(new MenuItem());
            }

            // Prepare root node. It represents the MenuRecord.
            var menu = items.First().Menu;
            var rootItem = new MenuItem
            {
                Text = menu.GetLocalized(x => x.Title)
            };
            var root = new TreeNode<MenuItem>(rootItem)
            {
                Id = menu.SystemName
            };

            var parent = root;
            MenuItemRecord prevItem = null;

            items = items.SortForTree(includeItemsWithoutExistingParent);

            foreach (var item in items)
            {
                // Get parent.
                if (prevItem != null)
                {
                    if (item.ParentItemId != parent.Value.EntityId)
                    {
                        if (item.ParentItemId == prevItem.Id)
                        {
                            // Level +1.
                            parent = parent.LastChild;
                        }
                        else
                        {
                            // Level -x.
                            while (!parent.IsRoot)
                            {
                                if (parent.Value.EntityId == item.ParentItemId)
                                {
                                    break;
                                }
                                parent = parent.Parent;
                            }
                        }
                    }
                }

                // Add to parent.
                if (item.ProviderName.HasValue() && itemProviders.TryGetValue(item.ProviderName, out var provider))
                {
                    provider.Value.Append(new MenuItemProviderRequest
                    {
                        Origin = origin,
                        Parent = parent,
                        Entity = item
                    });

                    prevItem = item;
                }
            }

            return root;
        }

        private static IList<MenuItemRecord> SortForTree(this IEnumerable<MenuItemRecord> items, bool includeItemsWithoutExistingParent)
        {
            var result = new List<MenuItemRecord>();

            SortChildItems(0);

            if (includeItemsWithoutExistingParent && result.Count != items.Count())
            {
                foreach (var item in items)
                {
                    if (result.FirstOrDefault(x => x.Id == item.Id) == null)
                    {
                        result.Add(item);
                    }
                }
            }

            return result;

            void SortChildItems(int parentItemId)
            {
                var childItems = items.Where(x => x.ParentItemId == parentItemId).ToArray();
                foreach (var item in childItems)
                {
                    result.Add(item);
                    SortChildItems(item.Id);
                }
            }
        }
    }
}
