# MemoD11 Drawing Migration Plan

## Goals

- Move drawing support to `MemoD11`.
- Keep drawing logic in data/model code as much as possible.
- Use `WriteableBitmap` throughout the drawing pipeline.
- Store point coordinates as normalized doubles relative to the original bitmap's physical pixel size.
- Store brush thickness in original-image physical pixels.
- Replace snapshot-based undo with operation-based undo/redo.
- Prepare the model for future object types such as text.
- Remove old `Memo` drawing code after `MemoD11` is functional.

## Data Model

### Document

`DrawingDocumentData`

- `SourcePixelWidth`
- `SourcePixelHeight`
- `Objects`
- `Operations`
- `AppliedOperationCount`

The document owns both content and history. `SceneItem` should eventually carry only one drawing/markup document field instead of separate document/history fields.

### Objects

`DrawingObjectData`

- `Id`
- `Kind`
- `IsDeleted`

First concrete type:

`DrawingStrokeData`

- `SizePx`
- `Points`
- `Color`

Future object types can be added without changing the operation stack design.

### Operations

`DrawingOperationData`

- `Kind`

Concrete operations:

- `AddDrawingObjectsOperationData`
- `RemoveDrawingObjectsOperationData`
- `UpdateDrawingObjectOperationData`

Undo/redo works by replaying operations against persistent object ids.

## Coordinate Rules

- Point coordinates are stored as normalized doubles relative to original bitmap physical pixel size.
- Brush size is stored in original bitmap physical pixels.
- Display transforms convert normalized coordinates back into physical pixels, then the existing memo transform/scale displays them.

## Erase Semantics

- Erase deletes whole objects.
- Erase operations store removed object ids.
- Undo/redo only toggles object visibility/state and does not rerun hit testing.

## Rendering Strategy

Drawing is rendered as an overlay bitmap layer:

1. Build a `WriteableBitmap` overlay from the current visible drawing objects.
2. Upload that overlay as a texture in `MemoD11`.
3. Composite it with the source image in the D3D11 render path.

This avoids pushing stroke/path data directly into shaders and keeps the model extensible for text and other object types.

## UI Split

### `MemoD11`

- Owns interaction mode state.
- Uses a dedicated drawing partial.
- Converts mouse input into normalized points.
- Calls document APIs.
- Requests overlay refresh.

### `DrawPanel`

- Reused as the tool panel only.
- Placement logic remains external and reusable.

## Migration Steps

1. Replace `DrawingData.cs` with object/operation-based document logic.
2. Update `SceneItem` to carry a single drawing document field.
3. Add `MemoD11.Drawing.cs`.
4. Add drawing overlay generation/upload to `MemoD11`.
5. Switch save/export composition to optionally include drawing.
6. Remove old `Memo` drawing code and related compatibility paths.
