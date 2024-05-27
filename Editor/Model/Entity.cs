using System;
using System.Collections;
using System.Collections.Generic;
using Editor.Gui;
using Microsoft.Xna.Framework;

namespace Editor.Model
{
    public class Entity : IEnumerable<string>
    {
        private readonly HashSet<string> _properties;
        private readonly Dictionary<string, object> _propertyCurrentValues;
        public string Id { get; set; }
        public string TextureId { get; }
        public HashSet<string> Properties => _properties;
        public Entity(string id, string textureId)
        {
            Id = id;
            TextureId = textureId;
            _properties = new HashSet<string>(4);
            _propertyCurrentValues = new Dictionary<string, object>(4);
        }

        public void SetCurrentPropertyValue(Property property, object value)
        {
            Properties.Add(property);
            _propertyCurrentValues[property] = value;
        }
        public void SetCurrentPropertyValue(string propertyId, object value)
        {
            Properties.Add(propertyId);
            _propertyCurrentValues[propertyId] = value;
        }
        public T GetCurrentPropertyValue<T>(Property property)
        {
            return (T)_propertyCurrentValues[property];
        }
        public T GetCurrentPropertyValue<T>(string propertyId)
        {
            return (T)_propertyCurrentValues[propertyId];
        }
        public IEnumerator<string> GetEnumerator()
        {
            return Properties.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool IsBeingHovered(Vector2 mouseWorld)
        {
            Vector2 position = (Vector2)_propertyCurrentValues[EditorApplication.POSITION_PROPERTY]; // ok odio este sistema pero supongo que servira para algo
            float rotation = (float)_propertyCurrentValues[EditorApplication.ROTATION_PROPERTY];
            Vector2 size = EditorApplication.State.Textures[TextureId].FrameSize * (Vector2)_propertyCurrentValues[EditorApplication.SCALE_PROPERTY];

            return ImGuiEx.IsInsideRectangle(position, size, rotation, mouseWorld);
        }
    }
}