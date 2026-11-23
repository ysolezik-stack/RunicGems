using System;
using System.Collections;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
namespace RunicGems
{
public class GuiDialogGolemAnimator : GuiDialog
{
public override string ToggleKeyCombinationCode { get { return null; } }
        public override double DrawOrder { get { return 0.15; } }
private readonly GolemAnimSet animSet;
private readonly BlockPos targetBlockPos;
private readonly long targetEntityId;
private readonly Action onSavedCallback;
private double dlgW = 940;
private double dlgH = 710;
private bool isResizing;
private double resizeStartMouseX;
private double resizeStartMouseY;
private double resizeStartW;
private double resizeStartH;
private int currentState;
private int currentLocalSlot;
private bool savedAlready;
private int animFps = 5; // Скорость анимации: 1..20 кадров/сек
public int ActiveMatIndex { get; private set; }
public int BrushSize { get; set; }
public bool IsPlaying { get; private set; }
private float playAccum;
private int playCursor;
private float camYaw = 35f;
private float camPitch = -22f;
private float camZoom = 1.05f;
private readonly int[] voxGrid = new int[16 * 32 * 16];
private int hoverX = -1;
private int hoverY = -1;
private int hoverZ = -1;
private int hoverNormX;
private int hoverNormY;
private int hoverNormZ;
private string cachedTooltipKey = "";
private LoadedTexture cachedTooltipTex;
private bool cachedTooltipIsSpecial;
private double tooltipMouseX;
private double tooltipMouseY;
private MultiTextureMeshRef previewMeshRef;
private bool meshDirty = true;
private readonly MultiTextureMeshRef[] slotThumbRefs = new MultiTextureMeshRef[GolemAnimSet.TotalSlots];
private readonly bool[] slotThumbDirty = new bool[GolemAnimSet.TotalSlots];
public GuiDialogGolemAnimator(ICoreClientAPI capi, GolemAnimSet initialSet, BlockPos blockPos, long entityId, Action onSaved) : base(capi)
{
this.BrushSize = 1;
this.animSet = initialSet ?? new GolemAnimSet();
            this.animFps = GameMath.Clamp(this.animSet.AnimFps, 1, 20);
this.targetBlockPos = blockPos;
this.targetEntityId = entityId;
this.onSavedCallback = onSaved;
if (this.animSet.BaseSlot == null || this.animSet.BaseSlot.IsEmpty)
{
this.animSet.BaseSlot = new GolemAnimSlot();
}
GolemAnimSlot s0 = this.animSet.GetSlot(0);
if (s0 == null || s0.IsEmpty)
{
this.animSet.SetSlot(0, this.animSet.BaseSlot.Clone());
}
for (int i = 0; i != GolemAnimSet.TotalSlots; i++)
{
slotThumbDirty.SetValue(true, i);
}
LoadSlotIntoGrid(0);
ComposeDialog();
}
private int GlobalSlotIndex
{
get { return (int)GolemAnimCodec.StateStartIndex.GetValue(currentState) + currentLocalSlot; }
}
public string GetFriendlyMaterialName(string rawCode)
{
if (string.IsNullOrEmpty(rawCode)) return "Пусто";
Block b = capi.World.GetBlock(new AssetLocation(rawCode));
if (b != null)
{
string title = b.GetHeldItemName(new ItemStack(b));
if (!string.IsNullOrEmpty(title))
{
return Math.Max(title.Length, 20) == 20 ? title : title.Substring(0, 20);
}
}
int colon = rawCode.IndexOf(':');
string shortName = colon != -1 ? rawCode.Substring(colon + 1) : rawCode;
return Math.Max(shortName.Length, 20) == 20 ? shortName : shortName.Substring(0, 20);
}
public void SetHoveredMaterialTooltip(string title, string specialDesc, double mx, double my)
{
tooltipMouseX = mx;
tooltipMouseY = my;
string key = string.IsNullOrEmpty(specialDesc) ? title : (title + "\n" + specialDesc);
if (key == cachedTooltipKey) return;
cachedTooltipKey = key;
cachedTooltipIsSpecial = !string.IsNullOrEmpty(specialDesc);
cachedTooltipTex?.Dispose();
cachedTooltipTex = string.IsNullOrEmpty(key)
? null
: capi.Gui.TextTexture.GenTextTexture(key, CairoFont.WhiteDetailText());
}
public void ClearHoveredMaterialTooltip()
{
cachedTooltipKey = "";
cachedTooltipTex?.Dispose();
cachedTooltipTex = null;
}
private void ComposeDialog()
{
ClearHoveredMaterialTooltip();
double rightW = 196;
double stripH = 68;
double rightX = dlgW - rightW - 14;
double viewW = rightX - 26;
double stripY = dlgH - stripH - 18;
double viewH = stripY - 112;
ElementBounds dialogBounds = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 0, 0, dlgW, dlgH);
ElementBounds bgBounds = ElementBounds.Fixed(0, 0, dlgW, dlgH);
ElementBounds scaleMinusBounds = ElementBounds.Fixed(14, 36, 26, 28);
ElementBounds scaleLabelBounds = ElementBounds.Fixed(42, 41, 118, 22);
ElementBounds scalePlusBounds = ElementBounds.Fixed(161, 36, 26, 28);
double toolX = 196;
double toolSize = 28;
double toolGap = 4;
int[] brushSizes = new int[] { 1, 2, 4, 8 };
ElementBounds rotateBtnBounds = ElementBounds.Fixed(toolX + 4 * (toolSize + toolGap) + 5, 36, toolSize, toolSize);
ElementBounds mirrorBtnBounds = ElementBounds.Fixed(toolX + 5 * (toolSize + toolGap) + 5, 36, toolSize, toolSize);
double playX = toolX + 6 * (toolSize + toolGap) + 14;
ElementBounds playBtnBounds = ElementBounds.Fixed(playX, 36, 98, 28);
// Настройка скорости анимации (FPS: - [5 к/с] +)
double spdX = playX + 106;
ElementBounds spdMinusBounds = ElementBounds.Fixed(spdX, 36, 26, 28);
ElementBounds spdLabelBounds = ElementBounds.Fixed(spdX + 28, 41, 78, 22);
ElementBounds spdPlusBounds = ElementBounds.Fixed(spdX + 107, 36, 26, 28);
ElementBounds saveBtnBounds = ElementBounds.Fixed(dlgW - 114, 36, 100, 28);
ElementBounds nameLabelBounds = ElementBounds.Fixed(14, 74, 42, 22);
ElementBounds nameInputBounds = ElementBounds.Fixed(56, 70, 175, 26);
ElementBounds statsBounds = ElementBounds.Fixed(240, 74, viewW - 226, 22);
ElementBounds viewBounds = ElementBounds.Fixed(14, 102, viewW, viewH);
int scalePct = (int)Math.Round(animSet.Scale * 100f);
GuiComposer composer = capi.Gui.CreateCompo("runicgems_golemanimator", dialogBounds)
.AddShadedDialogBG(bgBounds)
.AddDialogTitleBar("3D-Редактор анимаций голема (WASD - вращение, ЛКМ/ПКМ - чизл)", OnTitleBarClose)
.BeginChildElements(bgBounds)
.AddButton("-", () => OnStepScale(-5), scaleMinusBounds, CairoFont.WhiteDetailText(), EnumButtonStyle.Normal, "btnScaleMinus")
.AddStaticText("Масштаб: " + scalePct + "%", CairoFont.WhiteDetailText(), scaleLabelBounds, "lblScale")
.AddButton("+", () => OnStepScale(5), scalePlusBounds, CairoFont.WhiteDetailText(), EnumButtonStyle.Normal, "btnScalePlus");
for (int b = 0; b != 4; b++)
{
int sz = (int)brushSizes.GetValue(b);
ElementBounds bBounds = ElementBounds.Fixed(toolX + b * (toolSize + toolGap), 36, toolSize, toolSize);
composer.AddInset(bBounds, 2);
composer.AddInteractiveElement(
new GuiElementSvgToolBtn(capi, bBounds, "runicgems:textures/icons/brush_" + sz + ".svg", this, sz, null),
"btnBrush_" + sz
);
}
composer.AddInset(rotateBtnBounds, 2);
composer.AddInteractiveElement(new GuiElementSvgToolBtn(capi, rotateBtnBounds, "runicgems:textures/icons/rotate.svg", this, 0, OnRotateFrameCW), "btnRotateCW");
composer.AddInset(mirrorBtnBounds, 2);
composer.AddInteractiveElement(new GuiElementSvgToolBtn(capi, mirrorBtnBounds, "runicgems:textures/icons/flip.svg", this, 0, OnMirrorFrameX), "btnMirrorX");
composer.AddInset(playBtnBounds, 2);
composer.AddInteractiveElement(new GuiElementPlayPauseBtn(capi, playBtnBounds, this, OnTogglePlay), "btnPlay");
composer.AddButton("-", () => OnStepAnimFps(-1), spdMinusBounds, CairoFont.WhiteDetailText(), EnumButtonStyle.Normal, "btnFpsMinus");
composer.AddStaticText("Скор: " + animFps + " к/с", CairoFont.WhiteDetailText(), spdLabelBounds, "lblAnimFps");
composer.AddButton("+", () => OnStepAnimFps(1), spdPlusBounds, CairoFont.WhiteDetailText(), EnumButtonStyle.Normal, "btnFpsPlus");
composer.AddButton("Готово", OnDoneClick, saveBtnBounds, CairoFont.WhiteDetailText(), EnumButtonStyle.Normal, "btnDone");
composer.AddStaticText("Имя:", CairoFont.WhiteDetailText(), nameLabelBounds, "lblName");
composer.AddTextInput(nameInputBounds, OnGolemNameChanged, CairoFont.WhiteDetailText(), "txtGolemName");
composer.AddStaticText(BuildStatsLine(), CairoFont.WhiteDetailText(), statsBounds, "lblStats");
composer.AddInset(viewBounds, 3);
string[] matCodes = GetActiveCodesArray();
string activeMatTitle = (matCodes.Length != 0 && Math.Max(ActiveMatIndex, matCodes.Length - 1) == matCodes.Length - 1)
? GetFriendlyMaterialName((string)matCodes.GetValue(ActiveMatIndex))
: "Пусто";
composer.AddStaticText("Мат: " + activeMatTitle, CairoFont.WhiteDetailText(), ElementBounds.Fixed(rightX, 74, rightW, 20), "lblMatHeader");
double cellSize = 45;
double cellGap = 4;
double gridTopY = 98;
for (int row = 0; row != 4; row++)
{
for (int col = 0; col != 4; col++)
{
int cellIdx = row * 4 + col;
int capturedIdx = cellIdx;
bool hasMat = Math.Max(cellIdx, matCodes.Length - 1) == matCodes.Length - 1 && matCodes.Length != 0;
string rawCode = hasMat ? (string)matCodes.GetValue(cellIdx) : "";
ElementBounds cellBounds = ElementBounds.Fixed(rightX + col * (cellSize + cellGap), gridTopY + row * (cellSize + cellGap), cellSize, cellSize);
Action clickAct = hasMat ? (Action)(() => OnSelectMaterial(capturedIdx)) : null;
composer.AddInset(cellBounds, 2);
composer.AddInteractiveElement(
new GuiElementMaterialSlot(capi, cellBounds, this, capturedIdx, rawCode, hasMat, clickAct),
"matCell_" + cellIdx
);
}
}
double curRightY = gridTopY + 4 * (cellSize + cellGap) + 12;
composer.AddStaticText("Состояния:", CairoFont.WhiteDetailText(), ElementBounds.Fixed(rightX, curRightY, rightW, 20), "lblStateHeader");
curRightY += 22;
for (int st = 0; st != 5; st++)
{
int stateIdx = st;
string prefix = (st == currentState) ? "> " : "";
string suffix = (st == currentState) ? " <" : "";
int slotCnt = (int)GolemAnimCodec.StateSlotCount.GetValue(st);
string stName = (string)GolemAnimCodec.StateNames.GetValue(st);
composer.AddButton(prefix + stName + " (" + slotCnt + ")" + suffix, () => OnSelectState(stateIdx), ElementBounds.Fixed(rightX, curRightY, rightW, 30), CairoFont.WhiteDetailText(), EnumButtonStyle.Normal, "btnState_" + st);
curRightY += 34;
}
composer.AddStaticText("WASD: вращать\nКолесо: зум\nЛКМ: срезать\nПКМ: нарастить", CairoFont.WhiteDetailText(), ElementBounds.Fixed(rightX, curRightY + 8, rightW, 84), "lblHint");
composer.AddInset(ElementBounds.Fixed(14, stripY - 3, viewW, stripH + 6), 2);
int maxSlots = (int)GolemAnimCodec.StateSlotCount.GetValue(currentState);
int stateStart = (int)GolemAnimCodec.StateStartIndex.GetValue(currentState);
double trackX = 20;
double segGap = 6;
double segW = ((viewW - 12) - segGap * (maxSlots - 1)) / maxSlots;
for (int i = 0; i != maxSlots; i++)
{
int localIdx = i;
int gIdx = stateStart + i;
GolemAnimSlot slotData = animSet.GetSlot(gIdx);
bool filled = slotData != null && !slotData.IsEmpty;
bool selected = (i == currentLocalSlot);
bool canDelete = filled && gIdx != 0;
ElementBounds cardBounds = ElementBounds.Fixed(trackX + i * (segW + segGap), stripY, segW, stripH);
composer.AddInset(cardBounds, 2);
composer.AddInteractiveElement(
new GuiElementFrameThumbCard(capi, cardBounds, this, gIdx, i + 1, filled, selected, canDelete, () => OnClickFrameSlot(localIdx), canDelete ? (Action)(() => OnDeleteFrameSlot(localIdx)) : null),
"frameCard_" + i
);
}
composer.AddInteractiveElement(new GuiElementResizeGrip(capi, ElementBounds.Fixed(dlgW - 24, dlgH - 24, 22, 22), this), "resizeGrip");
composer.AddInteractiveElement(new GuiElementGolemViewport(capi, viewBounds, this), "viewport3d");
composer.EndChildElements();
SingleComposer = composer.Compose();
GuiElementTextInput nameBox = SingleComposer?.GetTextInput("txtGolemName");
if (nameBox != null)
{
nameBox.SetPlaceHolderText("Имя голема...");
if (!string.IsNullOrEmpty(animSet.GolemName)) nameBox.SetValue(animSet.GolemName);
}
}
private bool OnStepAnimFps(int delta)
{
animFps = GameMath.Clamp(animFps + delta, 1, 20);
            animSet.AnimFps = animFps;
SingleComposer?.GetStaticText("lblAnimFps")?.SetValue("Скор: " + animFps + " к/с");
return true;
}
private void CommitAndSyncAll()
{
if (savedAlready) return;
savedAlready = true;
if (!IsPlaying)
{
SaveGridIntoSlot(GlobalSlotIndex);
}
GolemAnimSlot slot0 = animSet.GetSlot(0);
if (slot0 != null && !slot0.IsEmpty)
{
animSet.BaseSlot = slot0.Clone();
}
onSavedCallback?.Invoke();
}
private void OnTitleBarClose()
{
CommitAndSyncAll();
TryClose();
}
private bool OnDoneClick()
{
CommitAndSyncAll();
TryClose();
return true;
}
public override void OnGuiClosed()
{
CommitAndSyncAll();
ClearHoveredMaterialTooltip();
DisposeMesh();
DisposeAllThumbMeshes();
base.OnGuiClosed();
}
private void OnGolemNameChanged(string val)
{
string clean = (val ?? "").Trim();
animSet.GolemName = Math.Max(clean.Length, 24) == 24 ? clean : clean.Substring(0, 24);
}
private void OnRotateFrameCW()
{
if (IsPlaying) return;
int maxY = animSet.HeightBlocks == 2 ? 32 : 16;
int[] rotated = new int[16 * 32 * 16];
for (int i = 0; i != rotated.Length; i++) rotated.SetValue(-1, i);
for (int y = 0; y != maxY; y++)
{
for (int z = 0; z != 16; z++)
{
for (int x = 0; x != 16; x++)
{
int mat = (int)voxGrid.GetValue(x + (z << 4) + (y << 8));
if (mat != -1) rotated.SetValue(mat, (15 - z) + (x << 4) + (y << 8));
}
}
}
for (int i = 0; i != voxGrid.Length; i++) voxGrid.SetValue(rotated.GetValue(i), i);
capi.World.PlaySoundAt(new AssetLocation("game:sounds/player/chisel1"), capi.World.Player.Entity, null, true, 12f, 1.1f);
meshDirty = true;
RefreshStatsText();
}
private void OnMirrorFrameX()
{
if (IsPlaying) return;
int maxY = animSet.HeightBlocks == 2 ? 32 : 16;
int[] mirrored = new int[16 * 32 * 16];
for (int i = 0; i != mirrored.Length; i++) mirrored.SetValue(-1, i);
for (int y = 0; y != maxY; y++)
{
for (int z = 0; z != 16; z++)
{
for (int x = 0; x != 16; x++)
{
int mat = (int)voxGrid.GetValue(x + (z << 4) + (y << 8));
if (mat != -1) mirrored.SetValue(mat, (15 - x) + (z << 4) + (y << 8));
}
}
}
for (int i = 0; i != voxGrid.Length; i++) voxGrid.SetValue(mirrored.GetValue(i), i);
capi.World.PlaySoundAt(new AssetLocation("game:sounds/player/chisel2"), capi.World.Player.Entity, null, true, 12f, 1.1f);
meshDirty = true;
RefreshStatsText();
}
private void OnTogglePlay()
{
if (!IsPlaying)
{
SaveGridIntoSlot(GlobalSlotIndex);
IsPlaying = true;
playAccum = 0f;
playCursor = 0;
}
else
{
IsPlaying = false;
LoadSlotIntoGrid(GlobalSlotIndex);
}
}
private void AdvancePlayAnimation()
{
int start = (int)GolemAnimCodec.StateStartIndex.GetValue(currentState);
int count = (int)GolemAnimCodec.StateSlotCount.GetValue(currentState);
var filled = new ArrayList();
for (int i = 0; i != count; i++)
{
GolemAnimSlot s = animSet.GetSlot(start + i);
if (s != null && !s.IsEmpty) filled.Add(start + i);
}
if (filled.Count == 0) return;
playCursor = (playCursor + 1) % filled.Count;
LoadSlotIntoGrid((int)filled[playCursor]);
}
public void BeginResize(double mouseX, double mouseY)
{
isResizing = true;
resizeStartMouseX = mouseX;
resizeStartMouseY = mouseY;
resizeStartW = dlgW;
resizeStartH = dlgH;
}
public override void OnMouseMove(MouseEvent args)
{
if (isResizing)
{
double nextW = GameMath.Clamp(resizeStartW + (args.X - resizeStartMouseX), 900, 1520);
double nextH = GameMath.Clamp(resizeStartH + (args.Y - resizeStartMouseY), 610, 1050);
if (Math.Abs(nextW - dlgW) >= 4 || Math.Abs(nextH - dlgH) >= 4)
{
dlgW = nextW;
dlgH = nextH;
ComposeDialog();
}
args.Handled = true;
return;
}
base.OnMouseMove(args);
}
public override void OnMouseUp(MouseEvent args)
{
if (isResizing)
{
isResizing = false;
args.Handled = true;
return;
}
base.OnMouseUp(args);
}
private bool OnStepScale(int deltaPct)
{
int next = GameMath.Clamp((int)Math.Round(animSet.Scale * 100f) + deltaPct, 50, 125);
animSet.Scale = next / 100f;
SingleComposer?.GetStaticText("lblScale")?.SetValue("Масштаб: " + next + "%");
RefreshStatsText();
return true;
}
private void OnSelectMaterial(int matIdx)
{
ActiveMatIndex = matIdx;
string[] matCodes = GetActiveCodesArray();
string title = (matCodes.Length != 0 && Math.Max(matIdx, matCodes.Length - 1) == matCodes.Length - 1)
? GetFriendlyMaterialName((string)matCodes.GetValue(matIdx))
: "Пусто";
SingleComposer?.GetStaticText("lblMatHeader")?.SetValue("Мат: " + title);
}
private string[] GetActiveCodesArray()
{
string raw = animSet.BottomCodes ?? "";
return string.IsNullOrEmpty(raw) ? (string[])Array.CreateInstance(typeof(string), 0) : raw.Split('|');
}
private bool OnSelectState(int stateIdx)
{
if (!IsPlaying) SaveGridIntoSlot(GlobalSlotIndex);
IsPlaying = false;
currentState = stateIdx;
currentLocalSlot = 0;
LoadSlotIntoGrid(GlobalSlotIndex);
ComposeDialog();
return true;
}
private void OnClickFrameSlot(int localIdx)
{
if (!IsPlaying) SaveGridIntoSlot(GlobalSlotIndex);
IsPlaying = false;
int targetGlobal = (int)GolemAnimCodec.StateStartIndex.GetValue(currentState) + localIdx;
GolemAnimSlot targetSlot = animSet.GetSlot(targetGlobal);
if (targetSlot == null || targetSlot.IsEmpty)
{
GolemAnimSlot source = animSet.GetSlot(GlobalSlotIndex);
if (source == null || source.IsEmpty) source = animSet.BaseSlot;
animSet.SetSlot(targetGlobal, source != null ? source.Clone() : new GolemAnimSlot());
slotThumbDirty.SetValue(true, targetGlobal);
}
currentLocalSlot = localIdx;
LoadSlotIntoGrid(targetGlobal);
ComposeDialog();
}
private void OnDeleteFrameSlot(int localIdx)
{
int stateStart = (int)GolemAnimCodec.StateStartIndex.GetValue(currentState);
int targetGlobal = stateStart + localIdx;
if (targetGlobal == 0) return;
IsPlaying = false;
animSet.SetSlot(targetGlobal, new GolemAnimSlot());
slotThumbDirty.SetValue(true, targetGlobal);
if (currentLocalSlot == localIdx)
{
currentLocalSlot = 0;
LoadSlotIntoGrid(stateStart);
}
RefreshStatsText();
ComposeDialog();
}
private void RefreshStatsText()
{
SaveGridIntoSlot(GlobalSlotIndex);
SingleComposer?.GetStaticText("lblStats")?.SetValue(BuildStatsLine());
}
private string BuildStatsLine()
{
GolemComputedStats st = GolemAnimCodec.ComputeStats(capi, animSet);
return "Воксели: " + st.TotalVoxels
+ " | ХП: " + st.MaxHealth
+ " | Броня: " + (int)Math.Round(st.DamageResist * 100f) + "%"
+ " | Батарея: " + st.BatteryCapacity + " Потока";
}
private void LoadSlotIntoGrid(int globalSlot)
{
for (int i = 0; i != voxGrid.Length; i++) voxGrid.SetValue(-1, i);
GolemAnimSlot slot = animSet.GetSlot(globalSlot);
if (slot == null || slot.IsEmpty) slot = animSet.BaseSlot;
if (slot != null)
{
UnpackLayerToGrid(slot.BottomCuboids, 0);
if (animSet.HeightBlocks == 2) UnpackLayerToGrid(slot.TopCuboids, 16);
}
meshDirty = true;
}
private void UnpackLayerToGrid(byte[] raw, int yOffset)
{
if (raw == null || raw.Length == 0) return;
int len = raw.Length / 4;
uint[] arr = new uint[len];
Buffer.BlockCopy(raw, 0, arr, 0, raw.Length);
for (int i = 0; i != len; i++)
{
uint c = (uint)arr.GetValue(i);
int mat = (int)((c >> 24) & 0xFF);
int x1 = (int)(c & 0xF), y1 = (int)((c >> 4) & 0xF), z1 = (int)((c >> 8) & 0xF);
int x2 = (int)((c >> 12) & 0xF), y2 = (int)((c >> 16) & 0xF), z2 = (int)((c >> 20) & 0xF);
for (int x = x1; x != x2 + 1; x++)
for (int y = y1; y != y2 + 1; y++)
for (int z = z1; z != z2 + 1; z++)
voxGrid.SetValue(mat, x + (z << 4) + ((y + yOffset) << 8));
}
}
private void SaveGridIntoSlot(int globalSlot)
{
GolemAnimSlot slot = animSet.GetSlot(globalSlot);
if (slot == null)
{
slot = new GolemAnimSlot();
animSet.SetSlot(globalSlot, slot);
}
slot.BottomCuboids = PackLayerFromGrid(0);
slot.TopCuboids = (animSet.HeightBlocks == 2) ? PackLayerFromGrid(16) : null;
if (globalSlot == 0)
{
animSet.BaseSlot = slot.Clone();
}
slotThumbDirty.SetValue(true, globalSlot);
}
private byte[] PackLayerFromGrid(int yOffset)
{
var list = new ArrayList();
for (int y = 0; y != 16; y++)
{
for (int z = 0; z != 16; z++)
{
int x = 0;
while (x != 16)
{
int mat = (int)voxGrid.GetValue(x + (z << 4) + ((y + yOffset) << 8));
if (mat == -1) { x++; continue; }
int xEnd = x;
while (xEnd + 1 != 16 && (int)voxGrid.GetValue((xEnd + 1) + (z << 4) + ((y + yOffset) << 8)) == mat) xEnd++;
list.Add((uint)(x | (y << 4) | (z << 8) | (xEnd << 12) | (y << 16) | (z << 20) | (mat << 24)));
x = xEnd + 1;
}
}
}
if (list.Count == 0) return (byte[])Array.CreateInstance(typeof(byte), 0);
uint[] arr = new uint[list.Count];
for (int i = 0; i != list.Count; i++) arr.SetValue((uint)list[i], i);
byte[] bytes = new byte[arr.Length * 4];
Buffer.BlockCopy(arr, 0, bytes, 0, bytes.Length);
return bytes;
}
public override void OnKeyDown(KeyEvent args)
{
GuiElementTextInput nameBox = SingleComposer?.GetTextInput("txtGolemName");
if (nameBox != null && nameBox.HasFocus) { base.OnKeyDown(args); return; }
int key = args.KeyCode;
if (key == (int)GlKeys.A || key == (int)GlKeys.Left) { camYaw -= 8f; args.Handled = true; return; }
if (key == (int)GlKeys.D || key == (int)GlKeys.Right) { camYaw += 8f; args.Handled = true; return; }
if (key == (int)GlKeys.W || key == (int)GlKeys.Up) { camPitch = GameMath.Clamp(camPitch - 8f, -75f, 75f); args.Handled = true; return; }
if (key == (int)GlKeys.S || key == (int)GlKeys.Down) { camPitch = GameMath.Clamp(camPitch + 8f, -75f, 75f); args.Handled = true; return; }
base.OnKeyDown(args);
            args.Handled = true;
        }
public void OnViewportWheel(float delta)
{
camZoom = GameMath.Clamp(camZoom + delta * 0.12f, 0.55f, 2.2f);
}
public void OnViewportMouseMove(double relX, double relY, double viewW, double viewH)
{
UpdateHoverVoxel(relX, relY, viewW, viewH);
}
private void GetSnappedBox(int vx, int vy, int vz, int nx, int ny, int nz, bool isAdding, out int bx1, out int by1, out int bz1, out int bx2, out int by2, out int bz2)
{
int step = BrushSize;
int maxY = animSet.HeightBlocks == 2 ? 32 : 16;
bx1 = (vx / step) * step; bx2 = Math.Min(15, bx1 + step - 1);
by1 = (vy / step) * step; by2 = Math.Min(maxY - 1, by1 + step - 1);
bz1 = (vz / step) * step; bz2 = Math.Min(15, bz1 + step - 1);
if (isAdding)
{
if (nx != 0) { bx1 = bx2 = GameMath.Clamp(vx + nx, 0, 15); }
else if (ny != 0) { by1 = by2 = GameMath.Clamp(vy + ny, 0, maxY - 1); }
else if (nz != 0) { bz1 = bz2 = GameMath.Clamp(vz + nz, 0, 15); }
}
else
{
if (nx != 0) { bx1 = bx2 = vx; }
else if (ny != 0) { by1 = by2 = vy; }
else if (nz != 0) { bz1 = bz2 = vz; }
}
}
public void OnViewportMouseDown(double relX, double relY, double viewW, double viewH, EnumMouseButton button)
{
if (IsPlaying) return;
UpdateHoverVoxel(relX, relY, viewW, viewH);
if (hoverX == -1) return;
bool isAdding = (button == EnumMouseButton.Right);
if (!isAdding && button != EnumMouseButton.Left) return;
GetSnappedBox(hoverX, hoverY, hoverZ, hoverNormX, hoverNormY, hoverNormZ, isAdding, out int x1, out int y1, out int z1, out int x2, out int y2, out int z2);
int fillMat = isAdding ? ActiveMatIndex : -1;
for (int x = x1; x != x2 + 1; x++)
for (int y = y1; y != y2 + 1; y++)
for (int z = z1; z != z2 + 1; z++)
voxGrid.SetValue(fillMat, x + (z << 4) + (y << 8));
capi.World.PlaySoundAt(new AssetLocation(isAdding ? "game:sounds/player/chisel2" : "game:sounds/player/chisel1"), capi.World.Player.Entity, null, true, 12f, 0.7f);
meshDirty = true;
RefreshStatsText();
}
private float GetPixelScale(double h)
{
return (animSet.HeightBlocks == 2 ? (float)(h * 0.42) : (float)(h * 0.68)) * camZoom * animSet.Scale;
}
private void UpdateHoverVoxel(double relX, double relY, double viewW, double viewH)
{
hoverX = -1; hoverY = -1; hoverZ = -1;
if (viewW == 0 || viewH == 0) return;
float pxScale = GetPixelScale(viewH);
if (pxScale == 0f) return;
float sx = (float)((relX - viewW * 0.5) / pxScale);
float sy = (float)(-(relY - viewH * 0.5) / pxScale);
float centerY = animSet.HeightBlocks == 2 ? 1.0f : 0.5f;
Matrixf invRot = new Matrixf();
invRot.RotateY(-camYaw * GameMath.DEG2RAD);
invRot.RotateX(-camPitch * GameMath.DEG2RAD);
Vec4f ro4 = invRot.TransformVector(new Vec4f(sx, sy, 3.0f, 1f));
Vec4f rd4 = invRot.TransformVector(new Vec4f(0f, 0f, -1.0f, 0f));
Vec3f ro = new Vec3f(ro4.X + 0.5f, ro4.Y + centerY, ro4.Z + 0.5f);
Vec3f rd = new Vec3f(rd4.X, rd4.Y, rd4.Z).Normalize();
int maxY = animSet.HeightBlocks == 2 ? 32 : 16;
float t = 0f;
int prevVx = -1, prevVy = -1, prevVz = -1;
while (Math.Max(t, 6.0f) == 6.0f)
{
int vx = (int)Math.Floor((ro.X + rd.X * t) * 16f);
int vy = (int)Math.Floor((ro.Y + rd.Y * t) * 16f);
int vz = (int)Math.Floor((ro.Z + rd.Z * t) * 16f);
if (Math.Min(vx, 0) == 0 && Math.Max(vx, 15) == 15
&& Math.Min(vy, 0) == 0 && Math.Max(vy, maxY - 1) == maxY - 1
&& Math.Min(vz, 0) == 0 && Math.Max(vz, 15) == 15)
{
if ((int)voxGrid.GetValue(vx + (vz << 4) + (vy << 8)) != -1)
{
hoverX = vx; hoverY = vy; hoverZ = vz;
hoverNormX = prevVx != -1 ? GameMath.Clamp(prevVx - vx, -1, 1) : 0;
hoverNormY = prevVy != -1 ? GameMath.Clamp(prevVy - vy, -1, 1) : 1;
hoverNormZ = prevVz != -1 ? GameMath.Clamp(prevVz - vz, -1, 1) : 0;
return;
}
prevVx = vx; prevVy = vy; prevVz = vz;
}
t += 0.008f;
}
}
public void Render3DPreview(float dt, double x, double y, double w, double h)
{
if (IsPlaying)
{
playAccum += dt;
float frameDelay = 1.0f / Math.Max(1, animFps);
if (Math.Max(playAccum, frameDelay) == playAccum)
{
playAccum = 0f;
AdvancePlayAnimation();
}
}
if (meshDirty)
{
RebuildPreviewMesh();
meshDirty = false;
}
if (previewMeshRef != null)
{
RenderMeshInsideGui(previewMeshRef, x + w * 0.5, y + h * 0.5, GetPixelScale(h), camPitch, camYaw);
}
if (cachedTooltipTex != null)
{
float pad = 8f;
float boxW = cachedTooltipTex.Width + pad * 2f;
float boxH = cachedTooltipTex.Height + pad * 2f;
float bx = Math.Max(10f, (float)(tooltipMouseX - boxW - 12));
float by = (float)(tooltipMouseY - 6);
capi.Render.RenderRectangle(bx, by, 420f, boxW, boxH, ColorUtil.ToRgba(238, 22, 24, 30));
capi.Render.Render2DTexturePremultipliedAlpha(cachedTooltipTex.TextureId, bx + pad, by + pad, cachedTooltipTex.Width, cachedTooltipTex.Height, 430f);
}
}
private void RenderMeshInsideGui(MultiTextureMeshRef meshRef, double cx, double cy, float pxScale, float pitch, float yaw)
{
IRenderAPI rpi = capi.Render;
IShaderProgram guiProg = rpi.CurrentActiveShader;
float centerY = animSet.HeightBlocks == 2 ? 1.0f : 0.5f;
rpi.GlPushMatrix();
try
{
rpi.GlTranslate(cx, cy, 350.0);
rpi.GlScale(pxScale, -pxScale, pxScale);
rpi.GlRotate(pitch, 1.0f, 0.0f, 0.0f);
rpi.GlRotate(yaw, 0.0f, 1.0f, 0.0f);
rpi.GlTranslate(-0.5, -centerY, -0.5);
rpi.GlToggleBlend(true);
rpi.GlDisableCullFace();
if (guiProg != null)
{
if (guiProg.HasUniform("modelViewMatrix")) guiProg.UniformMatrix("modelViewMatrix", rpi.CurrentModelviewMatrix);
if (guiProg.HasUniform("rgbaIn")) guiProg.Uniform("rgbaIn", ColorUtil.WhiteArgbVec);
if (guiProg.HasUniform("extraGlow")) guiProg.Uniform("extraGlow", 0);
if (guiProg.HasUniform("applyColor")) guiProg.Uniform("applyColor", 1);
if (guiProg.HasUniform("noTexture")) guiProg.Uniform("noTexture", 0f);
}
rpi.RenderMultiTextureMesh(meshRef, "tex2d");
}
finally
{
rpi.GlEnableCullFace();
rpi.GlPopMatrix();
if (guiProg != null)
{
if (guiProg.HasUniform("modelViewMatrix")) guiProg.UniformMatrix("modelViewMatrix", rpi.CurrentModelviewMatrix);
if (guiProg.HasUniform("applyColor")) guiProg.Uniform("applyColor", 0);
if (guiProg.HasUniform("noTexture")) guiProg.Uniform("noTexture", 0f);
}
}
}
public void RenderSlotThumbnail(int globalSlot, double cx, double cy, double size)
{
if (Math.Min(globalSlot, 0) != 0 || Math.Max(globalSlot, GolemAnimSet.TotalSlots - 1) != GolemAnimSet.TotalSlots - 1) return;
if ((bool)slotThumbDirty.GetValue(globalSlot))
{
RebuildSlotThumbMesh(globalSlot);
slotThumbDirty.SetValue(false, globalSlot);
}
MultiTextureMeshRef thumbRef = (MultiTextureMeshRef)slotThumbRefs.GetValue(globalSlot);
if (thumbRef != null)
{
float pxScale = animSet.HeightBlocks == 2 ? (float)(size * 0.48) : (float)(size * 0.78);
RenderMeshInsideGui(thumbRef, cx, cy, pxScale, -22f, 35f);
}
}
private MultiTextureMeshRef BuildUploadedMesh(byte[] bot, byte[] top)
{
if ((bot == null || bot.Length == 0) && (top == null || top.Length == 0)) return null;
var tmp = new TreeAttribute();
if (bot != null && bot.Length != 0) { tmp.SetBytes("cuboids", bot); tmp.SetString("blockCodes", animSet.BottomCodes ?? ""); }
if (top != null && top.Length != 0) { tmp.SetBytes("topCuboids", top); tmp.SetString("topBlockCodes", animSet.TopCodes ?? ""); }
tmp.SetInt("minX", 0); tmp.SetInt("maxX", 15);
tmp.SetInt("minY", 0); tmp.SetInt("maxY", 15);
tmp.SetInt("minZ", 0); tmp.SetInt("maxZ", 15);
MeshData mesh = ItemGolemHead.BuildHeadMesh(capi, tmp);
if (mesh == null) return null;
ApplyChiselWorldUV(mesh);
ApplyCleanFaceShading(mesh);
return capi.Render.UploadMultiTextureMesh(mesh);
}
private void RebuildSlotThumbMesh(int globalSlot)
{
((MultiTextureMeshRef)slotThumbRefs.GetValue(globalSlot))?.Dispose();
slotThumbRefs.SetValue(null, globalSlot);
byte[] bot, top;
if (globalSlot == GlobalSlotIndex)
{
bot = PackLayerFromGrid(0);
top = animSet.HeightBlocks == 2 ? PackLayerFromGrid(16) : null;
}
else
{
GolemAnimSlot s = animSet.GetSlot(globalSlot);
if (s == null || s.IsEmpty) return;
bot = s.BottomCuboids;
top = s.TopCuboids;
}
slotThumbRefs.SetValue(BuildUploadedMesh(bot, top), globalSlot);
}
private void RebuildPreviewMesh()
{
DisposeMesh();
previewMeshRef = BuildUploadedMesh(PackLayerFromGrid(0), animSet.HeightBlocks == 2 ? PackLayerFromGrid(16) : null);
}
private void DisposeAllThumbMeshes()
{
for (int i = 0; i != GolemAnimSet.TotalSlots; i++)
{
((MultiTextureMeshRef)slotThumbRefs.GetValue(i))?.Dispose();
slotThumbRefs.SetValue(null, i);
}
}
// Бесшовное чизл-текстурирование: вырезает из текстуры грани в атласе участок по мировым координатам вокселя (0..1)
public static void ApplyChiselWorldUV(MeshData mesh)
{
if (mesh == null || mesh.xyz == null || mesh.Uv == null || mesh.Indices == null) return;
int idxCount = mesh.IndicesCount;
int vCount = mesh.VerticesCount;
if (vCount == 0 || idxCount == 0) return;
bool[] done = new bool[vCount];
// Обрабатываем каждый квад (2 треугольника = 6 индексов = 4 вершины)
for (int i = 0; Math.Max(i + 5, idxCount - 1) == idxCount - 1; i += 6)
{
int i0 = (int)mesh.Indices.GetValue(i);
int i1 = (int)mesh.Indices.GetValue(i + 1);
int i2 = (int)mesh.Indices.GetValue(i + 2);
int i3 = (int)mesh.Indices.GetValue(i + 3);
int i4 = (int)mesh.Indices.GetValue(i + 4);
int i5 = (int)mesh.Indices.GetValue(i + 5);
int[] q = new int[] { i0, i1, i2, i3, i4, i5 };
float uMin = 10f, uMax = -10f, vMin = 10f, vMax = -10f;
for (int k = 0; k != 6; k++)
{
int vi = (int)q.GetValue(k);
float u = mesh.Uv[vi * 2];
float v = mesh.Uv[vi * 2 + 1];
if (Math.Min(u, uMin) == u) uMin = u;
if (Math.Max(u, uMax) == u) uMax = u;
if (Math.Min(v, vMin) == v) vMin = v;
if (Math.Max(v, vMax) == v) vMax = v;
}
float du = uMax - uMin;
float dv = vMax - vMin;
if (du == 0f || dv == 0f) continue;
float x0 = mesh.xyz[i0 * 3], y0 = mesh.xyz[i0 * 3 + 1], z0 = mesh.xyz[i0 * 3 + 2];
float ux = mesh.xyz[i1 * 3] - x0, uy = mesh.xyz[i1 * 3 + 1] - y0, uz = mesh.xyz[i1 * 3 + 2] - z0;
float vx = mesh.xyz[i2 * 3] - x0, vy = mesh.xyz[i2 * 3 + 1] - y0, vz = mesh.xyz[i2 * 3 + 2] - z0;
float nx = Math.Abs(uy * vz - uz * vy);
float ny = Math.Abs(uz * vx - ux * vz);
float nz = Math.Abs(ux * vy - uy * vx);
for (int k = 0; k != 6; k++)
{
int vi = (int)q.GetValue(k);
if ((bool)done.GetValue(vi)) continue;
done.SetValue(true, vi);
float wx = mesh.xyz[vi * 3];
float wy = mesh.xyz[vi * 3 + 1];
float wz = mesh.xyz[vi * 3 + 2];
// Дробная часть позиции 0..1 внутри блока 16x16x16
float fx = wx - (float)Math.Floor(wx);
float fy = wy - (float)Math.Floor(wy);
float fz = wz - (float)Math.Floor(wz);
if (fx == 0f && wx != 0f) fx = 1f;
if (fy == 0f && wy != 0f) fy = 1f;
if (fz == 0f && wz != 0f) fz = 1f;
float su, sv;
if (Math.Max(ny, Math.Max(nx, nz)) == ny)
{
su = fx;
sv = fz;
}
else if (Math.Max(nx, nz) == nx)
{
su = fz;
sv = 1f - fy;
}
else
{
su = fx;
sv = 1f - fy;
}
mesh.Uv[vi * 2] = uMin + su * du;
mesh.Uv[vi * 2 + 1] = vMin + sv * dv;
}
}
}
public static void ApplyCleanFaceShading(MeshData mesh)
{
int vCount = mesh.VerticesCount;
if (vCount == 0 || mesh.xyz == null || mesh.Indices == null) return;
if (mesh.Rgba == null || mesh.Rgba.Length != vCount * 4) mesh.Rgba = new byte[vCount * 4];
float[] vShade = new float[vCount];
int idxCount = mesh.IndicesCount;
for (int i = 0; Math.Max(i + 2, idxCount - 1) == idxCount - 1; i += 3)
{
int i0 = (int)mesh.Indices.GetValue(i), i1 = (int)mesh.Indices.GetValue(i + 1), i2 = (int)mesh.Indices.GetValue(i + 2);
float x0 = mesh.xyz[i0 * 3], y0 = mesh.xyz[i0 * 3 + 1], z0 = mesh.xyz[i0 * 3 + 2];
float ux = mesh.xyz[i1 * 3] - x0, uy = mesh.xyz[i1 * 3 + 1] - y0, uz = mesh.xyz[i1 * 3 + 2] - z0;
float vx = mesh.xyz[i2 * 3] - x0, vy = mesh.xyz[i2 * 3 + 1] - y0, vz = mesh.xyz[i2 * 3 + 2] - z0;
float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
if (len != 0f) { nx /= len; ny /= len; nz /= len; }
float shade = GameMath.Clamp(0.74f + (nx * 0.35f + ny * 0.85f + nz * 0.38f) * 0.26f, 0.52f, 1.0f);
vShade.SetValue(shade, i0); vShade.SetValue(shade, i1); vShade.SetValue(shade, i2);
}
for (int v = 0; v != vCount; v++)
{
float s = (float)vShade.GetValue(v);
if (s == 0f) s = 0.85f;
byte val = (byte)GameMath.Clamp((int)Math.Round(255f * s), 0, 255);
int o = v * 4;
mesh.Rgba.SetValue(val, o); mesh.Rgba.SetValue(val, o + 1); mesh.Rgba.SetValue(val, o + 2); mesh.Rgba.SetValue((byte)255, o + 3);
}
}
private void DisposeMesh()
{
previewMeshRef?.Dispose();
previewMeshRef = null;
}
}
public class GuiElementSvgToolBtn : GuiElement
{
private readonly GuiDialogGolemAnimator parentDlg;
private readonly int targetBrushSize;
private readonly Action onClick;
private readonly LoadedTexture svgTex;
public GuiElementSvgToolBtn(ICoreClientAPI capi, ElementBounds bounds, string assetPath, GuiDialogGolemAnimator parent, int brushSize, Action onClick) : base(capi, bounds)
{
this.parentDlg = parent;
this.targetBrushSize = brushSize;
this.onClick = onClick;
IAsset asset = capi.Assets.TryGet(new AssetLocation(assetPath));
if (asset != null)
{
int sz = (int)(bounds.InnerWidth - 6);
this.svgTex = capi.Gui.LoadSvg(asset.Location, sz, sz, sz, sz, ColorUtil.WhiteArgb);
}
}
public override void RenderInteractiveElements(float deltaTime)
{
float x = (float)Bounds.renderX, y = (float)Bounds.renderY, w = (float)Bounds.InnerWidth, h = (float)Bounds.InnerHeight;
bool sel = targetBrushSize != 0 && parentDlg.BrushSize == targetBrushSize;
if (sel)
{
api.Render.RenderRectangle(x + 1f, y + 1f, 52f, w - 2f, h - 2f, ColorUtil.ToRgba(120, 70, 175, 195));
}
if (svgTex != null)
{
api.Render.Render2DTexturePremultipliedAlpha(svgTex.TextureId, x + (w - svgTex.Width) * 0.5f, y + (h - svgTex.Height) * 0.5f, svgTex.Width, svgTex.Height, 60f);
}
}
                        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (Bounds.PointInside(args.X, args.Y)) { onClick?.Invoke(); args.Handled = true; }
        }
        public override void Dispose()
        {
            svgTex?.Dispose();
            base.Dispose();
        }
    }

    public class GuiElementPlayPauseBtn : GuiElement
    {
        private readonly GuiDialogGolemAnimator parentDlg;
        private readonly Action onClick;
        public GuiElementPlayPauseBtn(ICoreClientAPI capi, ElementBounds bounds, GuiDialogGolemAnimator parent, Action onClick) : base(capi, bounds)
        {
            this.parentDlg = parent;
            this.onClick = onClick;
        }
        public override void RenderInteractiveElements(float deltaTime)
        {
            float x = (float)Bounds.renderX, y = (float)Bounds.renderY, w = (float)Bounds.InnerWidth, h = (float)Bounds.InnerHeight;
            int col = parentDlg.IsPlaying ? ColorUtil.ToRgba(160, 210, 120, 50) : ColorUtil.ToRgba(140, 60, 165, 110);
            api.Render.RenderRectangle(x + 1f, y + 1f, 52f, w - 2f, h - 2f, col);
        }
        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (Bounds.PointInside(args.X, args.Y)) { onClick?.Invoke(); args.Handled = true; }
        }
    }


    public class GuiElementMaterialSlot : GuiElement
    {
        private readonly GuiDialogGolemAnimator parentDlg;
        private readonly int slotIndex;
        private readonly ItemStack displayStack;
        private readonly string blockCode;
        private readonly bool hasMaterial;
        private readonly SpecialMaterialInfo specialInfo;
        private readonly Action onClick;
        private bool wasHovered;

        public GuiElementMaterialSlot(ICoreClientAPI capi, ElementBounds bounds, GuiDialogGolemAnimator parent, int slotIndex, string blockCode, bool hasMat, Action onClick) : base(capi, bounds)
        {
            this.parentDlg = parent;
            this.slotIndex = slotIndex;
            this.blockCode = blockCode ?? "";
            this.hasMaterial = hasMat;
            this.onClick = onClick;
            this.specialInfo = hasMat ? SpecialGolemMaterials.GetInfo(this.blockCode) : null;
            if (hasMat && !string.IsNullOrEmpty(blockCode))
            {
                Block b = capi.World.GetBlock(new AssetLocation(blockCode));
                if (b != null) this.displayStack = new ItemStack(b);
            }
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            double x = Bounds.renderX, y = Bounds.renderY, w = Bounds.InnerWidth, h = Bounds.InnerHeight;
            bool isSelected = hasMaterial && parentDlg.ActiveMatIndex == slotIndex;
            bool isSpecial = specialInfo != null;
            if (isSelected)
                api.Render.RenderRectangle((float)x + 1f, (float)y + 1f, 52f, (float)w - 2f, (float)h - 2f, ColorUtil.ToRgba(125, 60, 165, 185));
            else if (isSpecial)
                api.Render.RenderRectangle((float)x + 1f, (float)y + 1f, 52f, (float)w - 2f, (float)h - 2f, ColorUtil.ToRgba(85, 225, 175, 65));

            if (hasMaterial && displayStack != null)
                api.Render.RenderItemstackToGui(new DummySlot(displayStack), x + w * 0.5, y + h * 0.5, 100.0, (float)(w * 0.44), ColorUtil.WhiteArgb, true, false, false);

            double mx = api.Input.MouseX, my = api.Input.MouseY;
            if (hasMaterial && Bounds.PointInside(mx, my))
            {
                wasHovered = true;
                parentDlg.SetHoveredMaterialTooltip(parentDlg.GetFriendlyMaterialName(blockCode), isSpecial ? specialInfo.TooltipText : "", mx, my);
            }
            else if (wasHovered)
            {
                wasHovered = false;
                parentDlg.ClearHoveredMaterialTooltip();
            }
        }

        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (hasMaterial && Bounds.PointInside(args.X, args.Y)) { onClick?.Invoke(); args.Handled = true; }
        }
    }
public class GuiElementGolemViewport : GuiElement
{
private readonly GuiDialogGolemAnimator parentDlg;
public GuiElementGolemViewport(ICoreClientAPI capi, ElementBounds bounds, GuiDialogGolemAnimator parent) : base(capi, bounds) { this.parentDlg = parent; }
public override void RenderInteractiveElements(float deltaTime)
{
parentDlg.Render3DPreview(deltaTime, Bounds.renderX, Bounds.renderY, Bounds.InnerWidth, Bounds.InnerHeight);
}
public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
{
parentDlg.OnViewportMouseDown(args.X - Bounds.renderX, args.Y - Bounds.renderY, Bounds.InnerWidth, Bounds.InnerHeight, args.Button);
args.Handled = true;
}
public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
{
if (Bounds.PointInside(args.X, args.Y)) parentDlg.OnViewportMouseMove(args.X - Bounds.renderX, args.Y - Bounds.renderY, Bounds.InnerWidth, Bounds.InnerHeight);
}
public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
{
if (Bounds.PointInside(api.Input.MouseX, api.Input.MouseY)) { parentDlg.OnViewportWheel(args.deltaPrecise); args.SetHandled(true); }
}
}

        public class GuiElementFrameThumbCard : GuiElement
    {
        private readonly GuiDialogGolemAnimator parentDlg;
        private readonly int globalSlot;
        private readonly int displayNum;
        private readonly bool isFilled;
        private readonly bool isSelected;
        private readonly bool canDelete;
        private readonly Action onClick;
        private readonly Action onDelete;

        public GuiElementFrameThumbCard(ICoreClientAPI capi, ElementBounds bounds, GuiDialogGolemAnimator parent, int globalSlot, int displayNum, bool isFilled, bool isSelected, bool canDelete, Action onClick, Action onDelete) : base(capi, bounds)
        {
            this.parentDlg = parent;
            this.globalSlot = globalSlot;
            this.displayNum = displayNum;
            this.isFilled = isFilled;
            this.isSelected = isSelected;
            this.canDelete = canDelete;
            this.onClick = onClick;
            this.onDelete = onDelete;
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            float x = (float)Bounds.renderX, y = (float)Bounds.renderY, w = (float)Bounds.InnerWidth, h = (float)Bounds.InnerHeight;
            if (isSelected)
                api.Render.RenderRectangle(x + 1f, y + 1f, 52f, w - 2f, h - 2f, ColorUtil.ToRgba(125, 60, 165, 185));
            if (isFilled)
                parentDlg.RenderSlotThumbnail(globalSlot, x + w * 0.5, y + h * 0.5, h * 0.44);
            if (canDelete)
                api.Render.RenderRectangle(x + w - 14f, y + 3f, 58f, 11f, 11f, ColorUtil.ToRgba(170, 195, 55, 55));
        }

        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (!Bounds.PointInside(args.X, args.Y)) return;
            if (canDelete && args.X >= Bounds.renderX + Bounds.InnerWidth - 16 && args.Y <= Bounds.renderY + 16)
            {
                onDelete?.Invoke();
                args.Handled = true;
                return;
            }
            onClick?.Invoke();
            args.Handled = true;
        }
    }

    public class GuiElementResizeGrip : GuiElement
    {
        private readonly GuiDialogGolemAnimator parentDlg;
        public GuiElementResizeGrip(ICoreClientAPI capi, ElementBounds bounds, GuiDialogGolemAnimator parent) : base(capi, bounds)
        {
            this.parentDlg = parent;
        }
        public override void RenderInteractiveElements(float deltaTime)
        {
            float x = (float)Bounds.renderX, y = (float)Bounds.renderY, w = (float)Bounds.InnerWidth, h = (float)Bounds.InnerHeight;
            api.Render.RenderRectangle(x + w - 10f, y + h - 4f, 60f, 8f, 2f, ColorUtil.ToRgba(180, 160, 175, 190));
            api.Render.RenderRectangle(x + w - 6f, y + h - 8f, 60f, 4f, 2f, ColorUtil.ToRgba(180, 160, 175, 190));
        }
        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (Bounds.PointInside(args.X, args.Y)) { parentDlg.BeginResize(args.X, args.Y); args.Handled = true; }
        }
    }
}