using System;
using System.Collections;
using System.Collections.Generic;
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
            Vector2 position = (Vector2)_propertyCurrentValues[GameApplication.POSITION_PROPERTY]; // ok odio este sistema pero supongo que servira para algo
            float rotation = (float)_propertyCurrentValues[GameApplication.ROTATION_PROPERTY];
            Vector2 size = GameApplication.State.Textures[TextureId].FrameSize * (Vector2)_propertyCurrentValues[GameApplication.SCALE_PROPERTY];

            // Translate point to local coordinates of the rectangle
            double localX = mouseWorld.X - position.X;
            double localY = mouseWorld.Y - position.Y;

            // Rotate point around the rectangle center by the negative of the rectangle angle
            double cosAngle = Math.Cos(-rotation);
            double sinAngle = Math.Sin(-rotation);
            double rotatedX = localX * cosAngle - localY * sinAngle;
            double rotatedY = localX * sinAngle + localY * cosAngle;

            // Check if the rotated point is inside the unrotated rectangle
            double halfWidth = size.X / 2;
            double halfHeight = size.Y / 2;
            return Math.Abs(rotatedX) <= halfWidth && Math.Abs(rotatedY) <= halfHeight;
        }
    }
}