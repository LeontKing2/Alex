﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Alex.Gui.Common;
using Alex.Gui.Controls;
using Alex.Gui.Input;
using Alex.Gui.Input.Listeners;
using Microsoft.Xna.Framework;

namespace Alex.Gui
{
    public class UiRoot : UiContainer
    {

        public UiRoot(int? width, int? height) : base(width, height) { }
        public UiRoot() : this(null, null) { }

        private IHoverable _hoveredElement;
        private IClickable _clickedElement;

        private IInputManager _input;

        public void Activate(IInputManager input)
        {
            _input = input;

            _input.MouseListener.MouseUp += OnMouseUp;
            _input.MouseListener.MouseDown += OnMouseDown;
            _input.MouseListener.MouseMove += OnMouseMove;
        }

        public void Deactivate()
        {
            if (_input == null) return;

            _input.MouseListener.MouseUp -= OnMouseUp;
            _input.MouseListener.MouseDown -= OnMouseDown;
            _input.MouseListener.MouseMove -= OnMouseMove;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var element = FindElementAtPosition<IHoverable>(this, e.Position);

            if (_hoveredElement != element)
            {
                _hoveredElement?.InvokeMouseLeave(e);
                element?.InvokeMouseEnter(e);
            }
            else
            {
                element?.InvokeMouseMove(e);
            }

            _hoveredElement = element;
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            var element = FindElementAtPosition<IClickable>(this, e.Position);

            if (_clickedElement != element)
            {
                _clickedElement?.InvokeMouseUp(e);
            }

            _clickedElement = element;
            element?.InvokeMouseDown(e);
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            var element = FindElementAtPosition<IClickable>(this, e.Position);

            if (_clickedElement == element)
            {
                element?.InvokeMouseUp(e);
            }

            _clickedElement = null;
        }

        private static TUiElement FindElementAtPosition<TUiElement>(UiContainer container, Point position) where TUiElement : class
        {
            TUiElement element;

            var controls = container.Controls.ToArray();
            foreach (var control in controls)
            {
                if (control is UiContainer childContainer)
                {
                    element = FindElementAtPosition<TUiElement>(childContainer, position);
                    if (element != null)
                    {
                        return element;
                    }
                }

                element = control as TUiElement;
                if (element != null)
                {
                    if (control.OuterBounds.Contains(position))
                    {
                        return element;
                    }
                }
            }

            element = container as TUiElement;
            if (element != null)
            {
                if (container.OuterBounds.Contains(position))
                {
                    return element;
                }
            }

            return null;
        }

    }
}