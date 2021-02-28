﻿using System.Collections.Generic;
using Microsoft.Xna.Framework;
using RocketUI;

namespace Alex.Gamestates.Common
{
    public class ListSelectionStateBase<TGuiListItemContainer> : GuiMenuStateBase 
		where TGuiListItemContainer : SelectionListItem
    {
	    public override int BodyMinWidth => 356;

        protected TGuiListItemContainer[] Items => _items.ToArray();
        private List<TGuiListItemContainer> _items { get; } = new List<TGuiListItemContainer>();

	    protected TGuiListItemContainer SelectedItem => ListContainer.SelectedItem as TGuiListItemContainer;
        protected readonly SelectionList ListContainer;

        public ListSelectionStateBase() : base()
        {
	        Body.BackgroundOverlay = new Color(Color.Black, 0.35f);

	        AddRocketElement(ListContainer = new SelectionList()
            {
	            Anchor = Alignment.Fill,
				ChildAnchor = Alignment.TopFill,
            });
	        ListContainer.SelectedItemChanged += HandleSelectedItemChanged;
        }

	    protected override void OnUpdate(GameTime gameTime)
	    {
		    base.OnUpdate(gameTime);
	    }

	    public void AddItem(TGuiListItemContainer item)
        {
            _items.Add(item);
            ListContainer.AddChild(item);
        }
        
        public void RemoveItem(TGuiListItemContainer item)
        {
            ListContainer.RemoveChild(item);
            _items.Remove(item);
        }

	    private void HandleSelectedItemChanged(object sender, SelectionListItem item)
	    {
			OnSelectedItemChanged(item as TGuiListItemContainer);
	    }

	    protected virtual void OnSelectedItemChanged(TGuiListItemContainer newItem)
	    {

	    }

	    public void ClearItems()
	    {
		    foreach (var item in _items)
		    {
			    ListContainer.RemoveChild(item);
		    }
			_items.Clear();
	    }
    }
}
