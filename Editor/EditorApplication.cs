global using NVector2 = System.Numerics.Vector2;
global using Vector2 = Microsoft.Xna.Framework.Vector2;
using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Editor.Geometry;
using Editor.Graphics;
using Editor.Graphics.Grid;
using Editor.Gui;
using Editor.Model;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rune.MonoGame;

namespace Editor
{
    public class State
    {
        private FrozenDictionary<string, Property> frozenPropDef = FrozenDictionary<string, Property>.Empty;
        public IDictionary<string, Property> PropertyDefinitions
        {
            get => frozenPropDef;
            set => frozenPropDef = value.ToFrozenDictionary();
        }
        public Dictionary<string, TextureFrame> Textures { get; set; } = new Dictionary<string, TextureFrame>();
        public Dictionary<string, Entity> Entities { get; set; } = new Dictionary<string, Entity>();
        public Animator Animator { get; set; } = new Animator();
    }

    public class EditorApplication : Game
    {
        private static DragAction currentDragAction;
        private SpriteBatch _spriteBatch;
        private PrimitiveBatch _primitiveBatch;

        public static State State;

        private DynamicGrid _grid;
        public Camera camera;
        private ImGuiRenderer _imguiRenderer;
        private MouseState previousMouseState;
        private bool nextFrameSave;

        // view state

        private ImGuiEx.FilePickerDefinition _openFdDefinition;

        public string selectedEntityId = string.Empty;
        public string selectedTextureId = string.Empty;
        public string hoveredentityId = string.Empty;
        public static EditorApplication Instance;
        private Vector2 previousMouseWorld, mouseWorld, mouseWorldDelta;
        private bool isRotating;
        /// <summary>
        /// 0 = Mostrar posiciones adyacentes
        /// 1 = Mostrar rotaciones adyacentes
        /// 2 = Confirmar nuevo proyecto
        /// 3 = Mostrar frame nuevo al mover keyframes
        /// 4 = Reproducir al seleccionar keyframe
        /// </summary>
        private BitArray settingsFlags = new BitArray(12);
        private string dragEntityWorldId = string.Empty;
        public const string POSITION_PROPERTY = "Position";
        public const string SCALE_PROPERTY = "Scale";
        public const string ROTATION_PROPERTY = "Rotation";
        public const string FRAMEINDEX_PROPERTY = "FrameIndex";

        public EditorApplication()
        {
            new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1400,
                PreferredBackBufferHeight = 768,
                IsFullScreen = false
            };

            Content.RootDirectory = "Content";
            Window.AllowUserResizing = true;
            IsFixedTimeStep = true;
            IsMouseVisible = true;
            Instance = this;
        }

        protected override void Initialize()
        {
            if (File.Exists("./settings.dat"))
            {
                using (var fs = File.OpenRead("./settings.dat"))
                {
                    using (BinaryReader reader = new BinaryReader(fs))
                    {
                        if (reader.BaseStream.Length >= 2)
                        {
                            settingsFlags = new BitArray(reader.ReadBytes(2));
                        }
                    }
                }
            }
            else
            {
                settingsFlags = new BitArray(new bool[] { true, true, true, true, false });

            }
            _grid = new DynamicGrid(new DynamicGridSettings() { GridSizeInPixels = 32 });

            camera = new Camera();

            // offset a bit to show origin at correct position
            camera.Move((Vector3.UnitX - Vector3.UnitY) * 64);

            State = new State();
            InitializeDefaultState(State);

            base.Initialize();
        }

        private void ResetEditor(State state, bool addDefaultProperties = true)
        {
            InitializeDefaultState(state, addDefaultProperties);
            selectedEntityId = string.Empty;
            selectedTextureId = string.Empty;
            hoveredentityId = string.Empty;
        }

        private void InitializeDefaultState(State state, bool addDefaultProperties = true)
        {
            state.Animator.AddInterpolator<Vector2>(
                (fraction, first, second) => first + (second - first) * fraction,
                (fraction, values) => ImGuiEx.CubicHermiteInterpolate(values, fraction));
            state.Animator.AddInterpolator<int>(
                (fraction, first, second) => (int)(first + (second - first) * fraction),
                (fraction, values) => (int)ImGuiEx.CubicHermiteInterpolate(values.Select(v => (float)v).ToArray(), fraction));
            state.Animator.AddInterpolator<float>(
                (fraction, first, second) => first + (second - first) * fraction,
                (fraction, values) => ImGuiEx.CubicHermiteInterpolate(values, fraction));
            state.Animator.OnKeyframeChanged += () =>
            {
                foreach (var entity in State.Entities.Values)
                {
                    foreach (var propertyId in entity)
                    {
                        var trackId = State.Animator.GetTrackKey(entity.Id, propertyId);
                        if (State.Animator.Interpolate(trackId, out object currentValue))
                            entity.SetCurrentPropertyValue(State.PropertyDefinitions[propertyId], currentValue);
                    }
                }
            };

            if (addDefaultProperties)
            {
                Dictionary<string, Property> properties = new Dictionary<string, Property>
                {
                    [POSITION_PROPERTY] = new Property(POSITION_PROPERTY, typeof(Vector2)),
                    [FRAMEINDEX_PROPERTY] = new Property(FRAMEINDEX_PROPERTY, typeof(int)),
                    [ROTATION_PROPERTY] = new Property(ROTATION_PROPERTY, typeof(float)),
                    [SCALE_PROPERTY] = new Property(SCALE_PROPERTY, typeof(Vector2))
                };
                state.PropertyDefinitions = properties;
            }
        }

        protected override void LoadContent()
        {
            _imguiRenderer = new ImGuiRenderer(this);
            ImGuiEx.IcoMoon.AddIconsToDefaultFont(14f);
            _imguiRenderer.RebuildFontAtlas();

            ImGui.StyleColorsDark();
            RangeAccessor<System.Numerics.Vector4> colors = ImGui.GetStyle().Colors;
            for (int i = 0; i < colors.Count; i++) // nome guta el azul >:(
            {
                ref System.Numerics.Vector4 color = ref colors[i];
                float r = color.X;
                float b = color.Z;
                color.Z = r * (1 + b - r);
                color.X = b;
                color.Y /= 1 + b - r;
            }

            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _primitiveBatch = new PrimitiveBatch(GraphicsDevice);

            base.LoadContent();
        }

        protected override void UnloadContent()
        {
            foreach (var texturesValue in State.Textures.Values)
            {
                texturesValue.Dispose();
            }

            base.UnloadContent();
        }

        private void Input()
        {
            MouseState newMouseState = Mouse.GetState();
            previousMouseWorld = camera.ScreenToWorld(previousMouseState.Position.ToVector2());
            previousMouseWorld.Y = -previousMouseWorld.Y;

            mouseWorld = camera.ScreenToWorld(newMouseState.Position.ToVector2());
            mouseWorld.Y = -mouseWorld.Y;

            mouseWorldDelta = mouseWorld - previousMouseWorld;
            if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
            {
                camera.Move(new Vector3(-mouseWorldDelta.X, mouseWorldDelta.Y, 0));
            }

            previousMouseState = newMouseState;
        }

        protected override void Update(GameTime gameTime)
        {
            ImGuiIOPtr io = ImGui.GetIO();

            if (!io.WantTextInput)
            {
                State.Animator.UpdateTimelineInputs();
            }

            bool onGrid = !io.WantCaptureMouse && !io.WantCaptureKeyboard;
            if (onGrid)
                Input();
            else
            {
            }
            _grid.CalculateBestGridSize(camera.Zoom);
            _grid.CalculateGridData(data =>
            {
                var viewport = GraphicsDevice.Viewport;
                data.GridDim = viewport.Height;

                var worldTopLeft = camera.ScreenToWorld(new Vector2(0, 0));
                var worldTopRight = camera.ScreenToWorld(new Vector2(viewport.Width, 0));
                var worldBottomRight = camera.ScreenToWorld(new Vector2(viewport.Width, viewport.Height));
                var worldBottomLeft = camera.ScreenToWorld(new Vector2(0, viewport.Height));

                Aabb bounds = new Aabb();
                bounds.Grow(worldTopLeft.X, worldTopLeft.Y, 0);
                bounds.Grow(worldTopRight.X, worldTopRight.Y, 0);
                bounds.Grow(worldBottomRight.X, worldBottomRight.Y, 0);
                bounds.Grow(worldBottomLeft.X, worldBottomLeft.Y, 0);

                return bounds;
            });

            State.Animator.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(32, 32, 32));

            _primitiveBatch.Begin(camera.View, camera.Projection);
            _grid.Render(_primitiveBatch, Matrix.Identity);
            _primitiveBatch.End();

            var translation = camera.View.Translation;
            var spriteBatchTransformation = Matrix.CreateTranslation(camera.lastSize.X / 2, camera.lastSize.Y / 2, 0) *
                                            Matrix.CreateTranslation(translation.X, -translation.Y, 0)
                                            * Matrix.CreateScale(camera.Zoom);

            _spriteBatch.Begin(transformMatrix: spriteBatchTransformation, samplerState: SamplerState.PointClamp);

            foreach (var entity in State.Entities.Values)
            {
                var position = entity.GetCurrentPropertyValue<Vector2>(POSITION_PROPERTY);
                var frameIndex = entity.GetCurrentPropertyValue<int>(FRAMEINDEX_PROPERTY);
                var rotation = entity.GetCurrentPropertyValue<float>(ROTATION_PROPERTY);
                var scale = entity.GetCurrentPropertyValue<Vector2>(SCALE_PROPERTY);

                var texture = State.Textures[entity.TextureId];
                int framesX = (int)(texture.Width / texture.FrameSize.X);

                int x = frameIndex % framesX;
                int y = frameIndex / framesX;

                var sourceRect = new Rectangle((int)(x * texture.FrameSize.X), (int)(y * texture.FrameSize.Y),
                    (int)texture.FrameSize.X, (int)texture.FrameSize.Y);

                _spriteBatch.Draw(texture, position, sourceRect, Color.White,
                    rotation, new Vector2(texture.Pivot.X, texture.Pivot.Y),
                    scale, SpriteEffects.None, 0f);
            }

            _spriteBatch.End();

            DrawUi(gameTime);
        }

        private void DrawSpriteBounds(string entityId, uint color)
        {
            var bgDrawList = ImGui.GetBackgroundDrawList();
            var entity = State.Entities[entityId];
            var texture = State.Textures[entity.TextureId];
            var position = entity.GetCurrentPropertyValue<Vector2>(POSITION_PROPERTY);
            var scale = entity.GetCurrentPropertyValue<Vector2>(SCALE_PROPERTY) * camera.Zoom;
            var rotation = entity.GetCurrentPropertyValue<float>(ROTATION_PROPERTY);

            var sp = camera.WorldToScreen(new Vector2(position.X, -position.Y));

            (NVector2 tl, NVector2 tr, NVector2 bl, NVector2 br) = GetQuads(sp.X, sp.Y, -texture.Pivot.X * scale.X, -texture.Pivot.Y * scale.Y, texture.FrameSize.X * scale.X, texture.FrameSize.Y * scale.Y, MathF.Sin(rotation), MathF.Cos(rotation));
            bgDrawList.AddQuad(tl, tr, br, bl, color);
        }
        private (NVector2 tl, NVector2 tr, NVector2 bl, NVector2 br) GetQuads(float x, float y, float dx, float dy, float w, float h, float sin, float cos)
        {
            NVector2 tl; NVector2 tr; NVector2 bl; NVector2 br;
            tl.X = x + dx * cos - dy * sin;
            tl.Y = y + dx * sin + dy * cos;
            tr.X = x + (dx + w) * cos - dy * sin;
            tr.Y = y + (dx + w) * sin + dy * cos;
            bl.X = x + dx * cos - (dy + h) * sin;
            bl.Y = y + dx * sin + (dy + h) * cos;
            br.X = x + (dx + w) * cos - (dy + h) * sin;
            br.Y = y + (dx + w) * sin + (dy + h) * cos;
            return (tl, tr, bl, br);
        }

        private unsafe void DrawUi(GameTime gameTime)
        {
            _imguiRenderer.BeforeLayout(gameTime);
            var drawList = ImGui.GetBackgroundDrawList();

            // Draw viewport overlays
            if (!string.IsNullOrEmpty(hoveredentityId))
                DrawSpriteBounds(hoveredentityId, Color.CornflowerBlue.PackedValue);
            else if (!string.IsNullOrEmpty(selectedEntityId))
                DrawSpriteBounds(selectedEntityId, Color.GreenYellow.PackedValue);
            if (!string.IsNullOrEmpty(selectedEntityId))
            {
                DrawSpriteBounds(selectedEntityId, Color.Red.PackedValue);

                var entity = State.Entities[selectedEntityId];
                var textureSize = State.Textures[entity.TextureId].FrameSize;
                var worldPos = entity.GetCurrentPropertyValue<Vector2>(POSITION_PROPERTY);
                worldPos.Y = -worldPos.Y;
                var position = camera.WorldToScreen(worldPos);
                var scale = entity.GetCurrentPropertyValue<Vector2>(SCALE_PROPERTY) * camera.Zoom;
                var rotation = entity.GetCurrentPropertyValue<float>(ROTATION_PROPERTY);
                (float sin, float cos) = MathF.SinCos(rotation);

                position.X += cos * (16f + textureSize.X / 2 * scale.X);
                position.Y += sin * (16f + textureSize.X / 2 * scale.X);
                ImFontGlyphPtr glyph = ImGui.GetIO().Fonts.Fonts[0].FindGlyph(ImGuiEx.IcoMoon.RotateIcon);
                float* glyphDataPtr = (float*)glyph.NativePtr; // jaja imgui.net no acepta el commit DE 4 AÑOS que añade bitfields el cual arregla el orden de ImFontGlyph

                (NVector2 tl, NVector2 tr, NVector2 bl, NVector2 br) = GetQuads(position.X, position.Y, -8, -8, 16, 16, sin, cos);
                float fontSize = ImGuiEx.IcoMoon.font.FontSize;

                NVector2 uv0 = new NVector2(glyphDataPtr[6], glyphDataPtr[7]);
                NVector2 uv1 = new NVector2(glyphDataPtr[8], glyphDataPtr[7]);
                NVector2 uv2 = new NVector2(glyphDataPtr[8], glyphDataPtr[9]);
                NVector2 uv3 = new NVector2(glyphDataPtr[6], glyphDataPtr[9]);
                drawList.AddImageQuad(_imguiRenderer.fontTextureId.Value, tl, tr, br, bl, uv0, uv1, uv2, uv3, Color.White.PackedValue);
            }
            if (ImGuiEx.selectedLink != null)
            {
                switch (ImGuiEx.selectedLink.track.PropertyId)
                {
                    case POSITION_PROPERTY:
                        foreach (var keyframe in ImGuiEx.selectedLink.Keyframes)
                        {
                            Vector2 position = camera.WorldToScreen((Vector2)keyframe.Value);
                            bool hover = ImGuiEx.IsInsideRectangle(position, new Vector2(10), MathHelper.PiOver4, ImGui.GetMousePos());
                            drawList.AddNgon(new NVector2(position.X, position.Y), 10, hover ? 0xBBBBBBBB : 0x66666666, 4);
                        }
                        break;
                }
            }
            const int hierarchyWindowWidth = 300;

            ImGui.SetNextWindowPos(new NVector2(0, GraphicsDevice.Viewport.Height - 250), ImGuiCond.Always);
            ImGui.SetNextWindowSize(NVector2.UnitX * (GraphicsDevice.Viewport.Width - hierarchyWindowWidth) + NVector2.UnitY * GraphicsDevice.Viewport.Height, ImGuiCond.Always);

            ImGui.Begin("timeline", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize);
            ImGuiEx.DrawUiTimeline(State.Animator);
            ImGui.End();

            ImGui.SetNextWindowPos(new NVector2(GraphicsDevice.Viewport.Width - hierarchyWindowWidth, 0), ImGuiCond.Always);
            ImGui.SetNextWindowSize(NVector2.UnitX * hierarchyWindowWidth +
                                    NVector2.UnitY * GraphicsDevice.Viewport.Height, ImGuiCond.Always);

            ImGui.Begin("Management", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize);
            {
                DrawUiActions();
                DrawUiHierarchyFrame();
                DrawUiProperties();
            }
            ImGui.End();

            _imguiRenderer.AfterLayout();

            if (ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow))
            {
                ImGui.SetNextFrameWantCaptureKeyboard(true);
                ImGui.SetNextFrameWantCaptureMouse(true);
            }
        }

        private void DrawUiActions()
        {
            var toolbarSize = NVector2.UnitY * (ImGui.GetTextLineHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y * 2);
            ImGui.Text($"{ImGuiEx.IcoMoon.HammerIcon} Actions");
            ImGui.BeginChild(1, toolbarSize, ImGuiChildFlags.FrameStyle);
            {
                if (nextFrameSave)
                {
                    _openFdDefinition = ImGuiEx.CreateFilePickerDefinition(Assembly.GetExecutingAssembly().Location, "Save", ".anim");
                    ImGui.OpenPopup("Save project");
                    nextFrameSave = false;
                }
                if (ImGuiEx.DelegateButton("New project", $"{ImGuiEx.IcoMoon.HammerIcon}", "New project"))
                {
                    if (settingsFlags[2])
                    {
                        ImGui.OpenPopup("Confirmar nuevo proyecto");
                    }
                    else
                    {
                        State = new State();
                        ResetEditor(State);
                    }
                }
                bool popupOpen = true;
                ImGui.SetNextWindowSize(new NVector2(400, 140));
                if (ImGui.BeginPopupModal("Confirmar nuevo proyecto", ref popupOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar))
                {
                    // if (ImGui.BeginChild(1, new NVector2(400, 120), ImGuiChildFlags.FrameStyle, ImGuiWindowFlags.ChildWindow | ImGuiWindowFlags.NoResize))
                    {
                        ImGui.Text("EsTAS seGURO de HACER un NUEVO proYECTO ?\neste menu es horrible");
                        if (ImGui.Button("si wn deja de gritar"))
                        {
                            State = new State();
                            ResetEditor(State);
                            ImGui.CloseCurrentPopup();
                        }
                        // ImGui.SameLine();
                        if (ImGui.Button("deja guardar >:("))
                        {
                            ImGui.CloseCurrentPopup();
                            nextFrameSave = true;
                        }
                        // ImGui.SameLine();
                        if (ImGui.Button("jaja no"))
                        {
                            ImGui.CloseCurrentPopup();
                        }
                        // ImGui.EndChild();
                    }
                    ImGui.EndPopup();
                }
                ImGui.SameLine();

                if (ImGuiEx.DelegateButton("Save project", $"{ImGuiEx.IcoMoon.FloppyDiskIcon}", "Save project"))
                {
                    _openFdDefinition = ImGuiEx.CreateFilePickerDefinition(Assembly.GetExecutingAssembly()
                        .Location, "Save", ".anim");
                    ImGui.OpenPopup("Save project");

                }
                DoPopup("Save project", ref _openFdDefinition, () =>
                {
                    if (!Path.HasExtension(_openFdDefinition.SelectedRelativePath))
                    {
                        _openFdDefinition.SelectedRelativePath += ".anim";
                    }

                    using (FileStream stream = File.Open(_openFdDefinition.SelectedRelativePath, FileMode.OpenOrCreate))
                    {
                        using (BinaryWriter writer = new BinaryWriter(stream))
                        {
                            writer.Write(1296649793);
                            writer.Write((byte)1);
                            writer.Write(State.Animator.FPS);
                            writer.Write(State.PropertyDefinitions.Count);
                            foreach (var propDefinition in State.PropertyDefinitions)
                            {
                                writer.Write(propDefinition.Key);
                                writer.Write(propDefinition.Value.Type.AssemblyQualifiedName);
                            }

                            writer.Write(State.Textures.Count);
                            foreach (var texture in State.Textures)
                            {
                                writer.Write(texture.Key);
                                writer.Write(texture.Value.Path);
                                writer.Write(texture.Value.FrameSize);
                                writer.Write(texture.Value.Pivot);
                            }

                            writer.Write(State.Entities.Count);
                            foreach (var entityId in State.Animator)
                            {
                                Entity entity = State.Entities[entityId];

                                writer.Write(entityId);
                                writer.Write(entity.TextureId);
                                Dictionary<string, AnimationTrack> tracks = new Dictionary<string, AnimationTrack>(State.Animator.EnumerateEntityTrackIds(entityId).Select(v =>
                                {
                                    AnimationTrack track = State.Animator.GetTrack(v);
                                    return new KeyValuePair<string, AnimationTrack>(track.PropertyId, track);
                                }));

                                writer.Write(entity.Properties.Count);
                                foreach (var property in entity)
                                {
                                    writer.Write(property);
                                    AnimationTrack animationTrack = tracks[property];

                                    writer.Write(animationTrack.Count);
                                    foreach (var keyframe in animationTrack)
                                    {
                                        writer.Write(keyframe.Frame);
                                        switch (keyframe.Value)
                                        {
                                            case Vector2 vector2:
                                                writer.Write(vector2);
                                                break;
                                            case float single:
                                                writer.Write(single);
                                                break;
                                            case int int32:
                                                writer.Write(int32);
                                                break;
                                        }
                                    }
                                    foreach (var link in animationTrack.links)
                                    {

                                    }
                                }
                            }
                        }
                    }
                });

                ImGui.SameLine();
                if (ImGuiEx.DelegateButton("Open project", $"{ImGuiEx.IcoMoon.FolderOpenIcon}", "Open project"))
                {
                    _openFdDefinition = ImGuiEx.CreateFilePickerDefinition(Assembly.GetExecutingAssembly()
                        .Location, "Open", ".anim");
                    ImGui.OpenPopup("Open project");
                }
                DoPopup("Open project", ref _openFdDefinition, () =>
                {
                    using (FileStream stream = File.OpenRead(_openFdDefinition.SelectedRelativePath))
                    {
                        using (BinaryReader reader = new BinaryReader(stream))
                        {
                            if (reader.ReadUInt32() == 1296649793)
                            {
                                ResetEditor(State = new State(), false);
                                switch (reader.ReadByte())
                                {
                                    default:
                                    case 0:
                                        State.Animator.FPS = reader.ReadInt32();
                                        int count = reader.ReadInt32();
                                        Dictionary<string, Property> properties = new Dictionary<string, Property>(count);
                                        for (int i = 0; i < count; i++)
                                        {
                                            string id = reader.ReadString();
                                            properties.Add(id, new Property(id, Type.GetType(reader.ReadString())));
                                        }
                                        State.PropertyDefinitions = properties;

                                        count = reader.ReadInt32();
                                        State.Textures.EnsureCapacity(count);
                                        for (int i = 0; i < count; i++)
                                        {
                                            string key = reader.ReadString();
                                            string path = reader.ReadString();
                                            var texture = Texture2D.FromFile(GraphicsDevice, path);

                                            State.Textures.Add(key, new TextureFrame(texture, path, reader.ReadNVector2(), reader.ReadNVector2()));
                                        }

                                        count = reader.ReadInt32();
                                        State.Entities.EnsureCapacity(count);
                                        for (int i = 0; i < count; i++)
                                        {
                                            string entityId = reader.ReadString();

                                            Entity entity = new Entity(entityId, reader.ReadString());
                                            State.Entities.Add(entityId, entity);

                                            int entityPropertyCount = reader.ReadInt32();
                                            for (int j = 0; j < entityPropertyCount; j++)
                                            {
                                                string propertyName = reader.ReadString();
                                                Property property = State.PropertyDefinitions[propertyName];
                                                entity.SetCurrentPropertyValue(propertyName, property.CreateInstance());

                                                int keyframeCount = reader.ReadInt32();
                                                Type type = property.Type;
                                                AnimationTrack track = new AnimationTrack(type, propertyName, entityId);
                                                for (int h = 0; h < keyframeCount; h++)
                                                {
                                                    int frame = reader.ReadInt32();
                                                    reader.ReadByte();
                                                    object data = null;
                                                    switch (type.FullName)
                                                    {
                                                        case "System.Int32":
                                                            data = reader.ReadInt32();
                                                            break;
                                                        case "System.Single":
                                                            data = reader.ReadSingle();
                                                            break;
                                                        case "Microsoft.Xna.Framework.Vector2":
                                                            data = reader.ReadVector2();
                                                            break;
                                                        default:
                                                            break;
                                                    }
                                                    if (data is not null)
                                                    {
                                                        Keyframe keyframe = new Keyframe(frame, data);
                                                        track.Add(keyframe);
                                                    }
                                                }
                                                State.Animator.AddTrack(entityId, track);
                                            }
                                        }
                                        break;
                                }
                                State.Animator.OnKeyframeChanged?.Invoke();
                            }
                        }
                    }
                });
                ImGui.SameLine();

                if (ImGuiEx.DelegateButton("Settings", $"{ImGuiEx.IcoMoon.SettingsIcon}", "Ve los ajust :)))"))
                {
                    ImGui.OpenPopup("Settings");
                }
                popupOpen = true;
                ImGui.SetNextWindowContentSize(NVector2.One * 600);
                if (ImGui.BeginPopupModal("Settings", ref popupOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar))
                {
                    Checkbox("Mostrar posiciones enlazadas", 0);
                    Checkbox("Mostrar rotaciones enlazadas", 1);
                    Checkbox("Confirmar nuevo proyecto", 2);
                    Checkbox("Mostrar frame nuevo al mover keyframes", 3);
                    Checkbox("Reproducir al seleccionar keyframe", 4);
                    if (ImGui.Button("OK!") || ImGui.IsKeyPressed(ImGuiKey.Enter))
                    {
                        if (!File.Exists("./settings.dat"))
                        {
                            File.Create("./settings.dat");
                        }
                        using (var fs = File.OpenWrite("./settings.dat"))
                        {
                            using (BinaryWriter writer = new BinaryWriter(fs))
                            {
                                byte[] bytes = new byte[2];
                                for (int i = 0; i < settingsFlags.Count; i++)
                                {
                                    if (settingsFlags.Get(i))
                                        bytes[i / 8] ^= (byte)(1 << (i % 8));
                                }
                                writer.Write(bytes);
                            }
                        }
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("No >:(") || ImGui.IsKeyPressed(ImGuiKey.Escape))
                    {
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                    void Checkbox(string text, int index)
                    {
                        bool value = settingsFlags.Get(index);
                        ImGui.Checkbox(text, ref value);
                        settingsFlags.Set(index, value);
                    }
                }
                ImGui.SameLine();
            }
            ImGui.EndChild();
        }

        private void DrawUiHierarchyFrame()
        {
            var size = ImGui.GetContentRegionAvail();
            var itemSpacing = ImGui.GetStyle().ItemSpacing + NVector2.UnitY * 8;
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, itemSpacing);

            ImGui.Text($"{ImGuiEx.IcoMoon.ListIcon} Hierarchy");
            ImGui.BeginChild(2, size - NVector2.UnitY * 256, ImGuiChildFlags.FrameStyle);
            {
                // create entity
                bool itemHovered = false;
                ImGui.Text($"{ImGuiEx.IcoMoon.ImagesIcon} Entities");
                ImGui.SameLine();

                if (State.Textures.Count > 0)
                {
                    if (ImGui.SmallButton($"{ImGuiEx.IcoMoon.PlusIcon}##1"))
                    {
                        ImGui.OpenPopup("Create entity");
                        ImGuiEx.DoEntityCreatorReset();
                    }
                }
                else
                {
                    ImGuiEx.DisabledButton($"{ImGuiEx.IcoMoon.PlusIcon}");
                }
                var propDef = State.PropertyDefinitions[POSITION_PROPERTY];
                var fiPropDef = State.PropertyDefinitions[FRAMEINDEX_PROPERTY];
                var rPropDef = State.PropertyDefinitions[ROTATION_PROPERTY];
                var sPropDef = State.PropertyDefinitions[SCALE_PROPERTY];

                ImGuiEx.DoEntityCreatorModal(State.Textures.Keys.ToArray(), (name, selectedTexture) =>
                {
                    Entity entity = new Entity(name, selectedTexture);

                    entity.SetCurrentPropertyValue(POSITION_PROPERTY, propDef.CreateInstance());
                    State.Animator.CreateTrack(propDef.Type, name, POSITION_PROPERTY);

                    entity.SetCurrentPropertyValue(FRAMEINDEX_PROPERTY, fiPropDef.CreateInstance());
                    State.Animator.CreateTrack(fiPropDef.Type, name, FRAMEINDEX_PROPERTY);

                    entity.SetCurrentPropertyValue(ROTATION_PROPERTY, rPropDef.CreateInstance());
                    State.Animator.CreateTrack(rPropDef.Type, name, ROTATION_PROPERTY);

                    entity.SetCurrentPropertyValue(SCALE_PROPERTY, Vector2.One);
                    State.Animator.CreateTrack(sPropDef.Type, name, SCALE_PROPERTY);

                    State.Entities[entity.Id] = entity;
                });

                // show all created entities
                ImGui.Indent();
                bool onGrid = !ImGui.GetIO().WantCaptureMouse && !ImGui.GetIO().WantCaptureKeyboard;
                bool clickedOnGrid = onGrid && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
                bool hasSelected = false, selectedSame = false;
                string oldSelectedEntityId = selectedEntityId;

                if (!string.IsNullOrEmpty(selectedEntityId))
                {
                    Entity entity = State.Entities[selectedEntityId];
                    Vector2 position = entity.GetCurrentPropertyValue<Vector2>(POSITION_PROPERTY);
                    if (!isRotating)
                    {
                        Vector2 rotatePosition = camera.WorldToScreen(position);

                        float rotation = entity.GetCurrentPropertyValue<float>(ROTATION_PROPERTY);
                        Vector2 scale = entity.GetCurrentPropertyValue<Vector2>(SCALE_PROPERTY) * camera.Zoom;
                        Vector2 textureSize = State.Textures[entity.TextureId].FrameSize;

                        (float sin, float cos) = MathF.SinCos(rotation);

                        rotatePosition.X += cos * (16f + textureSize.X / 2 * scale.X);
                        rotatePosition.Y -= sin * (16f + textureSize.X / 2 * scale.X);

                        rotatePosition = camera.ScreenToWorld(rotatePosition);

                        if (onGrid && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Vector2.DistanceSquared(rotatePosition, mouseWorld) < 64)
                        {
                            selectedSame = true;
                            isRotating = true;
                        }
                    }

                    if (isRotating)
                    {
                        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                        {
                            Vector2 diff = mouseWorld - position;
                            entity.SetCurrentPropertyValue(ROTATION_PROPERTY, MathF.Atan2(diff.Y, diff.X));
                        }
                        else
                        {
                            isRotating = false;
                        }
                    }
                }

                foreach (var entity in State.Entities.Values)
                {
                    if (!hasSelected && entity.IsBeingHovered(mouseWorld) && clickedOnGrid)
                    {
                        if (selectedEntityId != entity.Id)
                        {
                            selectedEntityId = entity.Id;
                            selectedEntityId = entity.Id;
                            hasSelected = true;
                        }
                        else
                        {
                            dragEntityWorldId = selectedEntityId;
                            selectedSame = true;
                        }
                    }
                    bool selected = selectedEntityId == entity.Id;
                    ImGui.Selectable(entity.Id, ref selected);

                    if (selected)
                    {
                        selectedTextureId = string.Empty;
                        selectedEntityId = entity.Id;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        itemHovered = true;
                        hoveredentityId = entity.Id;
                    }
                }
                if (hasSelected)
                {
                    dragEntityWorldId = selectedEntityId;
                }
                bool validDrag = !string.IsNullOrEmpty(dragEntityWorldId);
                if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) && validDrag)
                {
                    dragEntityWorldId = string.Empty;
                    validDrag = false;
                }
                if (clickedOnGrid && !hasSelected && !selectedSame)
                {
                    selectedEntityId = string.Empty;
                }

                if (onGrid && validDrag && !isRotating)
                {
                    Entity selectedEntity = State.Entities[dragEntityWorldId];
                    selectedEntity.SetCurrentPropertyValue(propDef, selectedEntity.GetCurrentPropertyValue<Vector2>(propDef) + mouseWorldDelta);
                }

                if (oldSelectedEntityId != selectedEntityId)
                {
                    ImGuiEx.ResetSavedInput();
                }
                ImGui.Unindent();

                if (!itemHovered)
                    hoveredentityId = string.Empty;

                // Add textures
                ImGui.Text($"{ImGuiEx.IcoMoon.TextureIcon} Textures");
                ImGui.SameLine();

                if (ImGui.SmallButton($"{ImGuiEx.IcoMoon.PlusIcon}##2"))
                {
                    _openFdDefinition = ImGuiEx.CreateFilePickerDefinition(Assembly.GetExecutingAssembly()
                        .Location, "Open", ".png");
                    ImGui.OpenPopup("Load texture");
                }

                DoPopup("Load texture", ref _openFdDefinition, () =>
                {
                    var key = Path.GetFileNameWithoutExtension(_openFdDefinition.SelectedFileName);
                    if (!State.Textures.ContainsKey(key))
                    {
                        var path = _openFdDefinition.SelectedRelativePath;
                        var texture = Texture2D.FromFile(GraphicsDevice, path);
                        State.Textures[key] = new TextureFrame(texture, path,
                            new NVector2(texture.Width, texture.Height),
                            new NVector2(texture.Width / 2, texture.Height / 2));
                    }
                });

                // show all loaded textures
                ImGui.Indent();
                foreach (var texture in State.Textures.Keys)
                {
                    bool selected = selectedTextureId == texture;
                    ImGui.Selectable(texture, ref selected);

                    if (selected)
                    {
                        selectedEntityId = string.Empty;
                        selectedTextureId = texture;
                    }
                }
                ImGui.Unindent();

                ImGui.TreePop();
            }
            ImGui.EndChild();
            ImGui.PopStyleVar();
        }

        private void DrawUiProperties()
        {
            void InsertKeyframe(string entityId, string propertyId)
            {
                var entity = State.Entities[entityId];
                var propDef = State.PropertyDefinitions[propertyId];
                var trackId = State.Animator.GetTrackKey(entityId, propertyId);
                var value = entity.GetCurrentPropertyValue<object>(propDef);

                State.Animator.InsertKeyframe(trackId, value);
            }

            ImGui.Text($"{ImGuiEx.IcoMoon.EqualizerIcon} Properties");
            ImGui.BeginChild(3, NVector2.UnitY * 208, ImGuiChildFlags.FrameStyle);
            if (!string.IsNullOrEmpty(selectedEntityId))
            {
                var selectedEntity = State.Entities[selectedEntityId];

                var tempEntityName = ImGuiEx.SavedInput(string.Empty, selectedEntity.Id);
                ImGui.SameLine();
                if (ImGui.Button("Rename") && !State.Entities.ContainsKey(tempEntityName))
                {
                    RenameEntity(selectedEntity, tempEntityName);
                    ImGuiEx.ResetSavedInput();
                }

                ImGui.Separator();

                ImGui.Columns(2);
                ImGui.SetColumnWidth(0, 28);
                if (ImGui.Button($"{ImGuiEx.IcoMoon.KeyIcon}##group"))
                {
                    foreach (var propertyId in selectedEntity)
                    {
                        InsertKeyframe(selectedEntityId, propertyId);
                    }
                }
                ImGui.NextColumn();
                ImGui.Text("All properties");
                ImGui.Separator();
                ImGui.NextColumn();

                var keyframeButtonId = 0;
                foreach (var propertyId in selectedEntity)
                {
                    ImGui.PushID(keyframeButtonId++);
                    if (ImGui.Button($"{ImGuiEx.IcoMoon.KeyIcon}"))
                        InsertKeyframe(selectedEntityId, propertyId);
                    ImGui.PopID();

                    ImGui.NextColumn();

                    var propDefinition = State.PropertyDefinitions[propertyId];
                    switch (propertyId)
                    {
                        case SCALE_PROPERTY:
                        case POSITION_PROPERTY:
                            Vector2 value = selectedEntity.GetCurrentPropertyValue<Vector2>(propDefinition);

                            var pos = new NVector2(value.X, value.Y);
                            ImGui.DragFloat2(propertyId, ref pos);

                            value.X = pos.X;
                            value.Y = pos.Y;

                            selectedEntity.SetCurrentPropertyValue(propDefinition, value);

                            break;
                        case FRAMEINDEX_PROPERTY:
                            int frameIndex = selectedEntity.GetCurrentPropertyValue<int>(propDefinition);

                            var texture = State.Textures[selectedEntity.TextureId];
                            int framesX = (int)(texture.Width / texture.FrameSize.X);
                            int framesY = (int)(texture.Height / texture.FrameSize.Y);

                            ImGui.SliderInt(propertyId, ref frameIndex, 0, framesX * framesY - 1);

                            selectedEntity.SetCurrentPropertyValue(propDefinition, frameIndex);
                            break;
                        case ROTATION_PROPERTY:
                            float rotation = selectedEntity.GetCurrentPropertyValue<float>(propDefinition);
                            float angleRotation = rotation * 180f / MathHelper.Pi;
                            ImGui.DragFloat(propertyId, ref angleRotation, 1, -360f, 360f, "%.0f deg", ImGuiSliderFlags.NoRoundToFormat);
                            rotation = angleRotation * MathHelper.Pi / 180f;
                            selectedEntity.SetCurrentPropertyValue(propDefinition, rotation);

                            break;
                    }

                    ImGui.NextColumn();
                }
                if (State.Animator.EntityHasKeyframeAtFrame(selectedEntityId, State.Animator.CurrentKeyframe)) //TODO: select interpolation type in menu
                {
                    // ImGui.ListBox("")
                }
            }
            else if (!string.IsNullOrEmpty(selectedTextureId))
            {
                var scale = 2f;
                var selectedTexture = State.Textures[selectedTextureId];
                var currentFrameSize = selectedTexture.FrameSize;
                var currentPivot = selectedTexture.Pivot;

                ImGui.DragFloat2("Framesize", ref currentFrameSize);
                ImGui.DragFloat2("Pivot", ref currentPivot);

                selectedTexture.FrameSize = currentFrameSize;
                selectedTexture.Pivot = currentPivot;

                var scaledFrameSize = currentFrameSize * scale;
                var scaledPivot = currentPivot * scale;

                ImGui.BeginChild(2, NVector2.UnitY * 154f, ImGuiChildFlags.FrameStyle);

                var contentSize = ImGui.GetContentRegionAvail();
                var center = ImGui.GetCursorScreenPos() + contentSize * 0.5f;
                var frameStart = center - scaledFrameSize * 0.5f;

                // draw frame size
                var drawList = ImGui.GetWindowDrawList();
                drawList.AddRect(frameStart, frameStart + scaledFrameSize, Color.GreenYellow.PackedValue);

                // horizontal line
                drawList.AddLine(center - NVector2.UnitX * scaledFrameSize * 0.5f,
                    center + NVector2.UnitX * scaledFrameSize * 0.5f,
                    Color.ForestGreen.PackedValue);

                // vertical line
                drawList.AddLine(center - NVector2.UnitY * scaledFrameSize * 0.5f,
                    center + NVector2.UnitY * scaledFrameSize * 0.5f,
                    Color.ForestGreen.PackedValue);

                // draw pivot
                drawList.AddCircleFilled(frameStart + scaledPivot, 4, Color.White.PackedValue);

                ImGui.EndChild();
            }

            ImGui.EndChild();
        }

        private void RenameEntity(Entity entity, string newName)
        {
            // re-add entity
            var oldName = entity.Id;
            State.Entities.Remove(oldName);
            State.Entities[newName] = entity;
            entity.Id = newName;

            if (State.Animator.ChangeEntityName(oldName, newName))
            {
                foreach (var property in entity)
                {
                    var oldId = State.Animator.GetTrackKey(oldName, property);
                    State.Animator.ChangeTrackId(newName, property, oldId);
                }
            }

            selectedEntityId = newName;
            if (!string.IsNullOrEmpty(hoveredentityId))
                hoveredentityId = newName;
            if (selectedEntityId == oldName)
                selectedEntityId = newName;
            if (dragEntityWorldId == oldName)
                dragEntityWorldId = newName;
        }

        private void DoPopup(string id, ref ImGuiEx.FilePickerDefinition fpd, Action onDone)
        {
            bool popupOpen = true;
            ImGui.SetNextWindowContentSize(NVector2.One * 400);
            if (ImGui.BeginPopupModal(id, ref popupOpen, ImGuiWindowFlags.NoResize))
            {
                if (ImGuiEx.DoFilePicker(ref fpd))
                    onDone?.Invoke();

                ImGui.EndPopup();
            }
        }
        public static void SetDragAction(DragAction action)
        {
            currentDragAction = action;
        }
    }
    public struct DragAction
    {
        public readonly Action OnRelease;
        public readonly Action<Vector2> OnMove;
        public readonly float DistanceForMove;
        public bool CallOnMove;
        public Vector2 ClickPosition, OldPos;
        public DragAction(Action onRelease, Action<Vector2> onMove, float distanceForMove = 0)
        {
            OnRelease = onRelease;
            OnMove = onMove;
            distanceForMove = MathF.Abs(distanceForMove);
            DistanceForMove = distanceForMove * distanceForMove;
            CallOnMove = distanceForMove == 0;
        }
        public void Update()
        {
            if (CallOnMove)
            {
                Vector2 diff = ImGui.GetCursorPos() - OldPos;
                if (diff.X != 0 && diff.Y != 0)
                    OnMove?.Invoke(diff);
                OldPos = ImGui.GetCursorPos();
            }
            else if (Vector2.DistanceSquared(ImGui.GetCursorPos(), ClickPosition) < DistanceForMove)
            {
                OnMove?.Invoke(ImGui.GetCursorPos() - ClickPosition);
                CallOnMove = true;
            }
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                OnRelease?.Invoke();
            }
        }
    }
}