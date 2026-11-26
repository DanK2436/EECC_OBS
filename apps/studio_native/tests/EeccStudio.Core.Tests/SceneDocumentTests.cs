using EeccStudio.Core.Models;
using Xunit;

namespace EeccStudio.Core.Tests;

public class SceneDocumentTests
{
    [Fact]
    public void Rotation_s_accumule_et_revient_dans_le_cercle()
    {
        var source = new StudioSource { RotationAngle = 270 };
        Assert.True(SourceTransform.Rotate(source, 90));
        Assert.Equal(0, source.RotationAngle);
        Assert.True(SourceTransform.Rotate(source, -90));
        Assert.Equal(270, source.RotationAngle);
    }

    [Fact]
    public void Une_source_verrouillee_ignore_miroir_et_reset()
    {
        var source = new StudioSource { IsLocked = true, RotationAngle = 90, FlipHorizontal = true };
        Assert.False(SourceTransform.FlipVertical(source));
        Assert.False(SourceTransform.Reset(source));
        Assert.Equal(90, source.RotationAngle);
        Assert.True(source.FlipHorizontal);
        Assert.False(source.FlipVertical);
    }

    [Fact]
    public void Annuler_puis_retablir_restaure_la_transformation()
    {
        var source = new StudioSource();
        var stack = new StudioUndoStack();
        var before = SourceTransform.Capture(source);
        SourceTransform.Rotate(source, 180);
        SourceTransform.FlipHorizontal(source);
        var after = SourceTransform.Capture(source);
        stack.Push("rotation", () => SourceTransform.Restore(source, before), () => SourceTransform.Restore(source, after));

        Assert.True(stack.TryUndo());
        Assert.Equal(0, source.RotationAngle);
        Assert.False(source.FlipHorizontal);
        Assert.True(stack.TryRedo());
        Assert.Equal(180, source.RotationAngle);
        Assert.True(source.FlipHorizontal);
    }

    [Fact]
    public void La_collection_survit_a_un_aller_retour_json()
    {
        var root = Path.Combine(Path.GetTempPath(), "eecc-scenes-" + Guid.NewGuid().ToString("N"));
        var path = SceneCollectionFile.PathFor(root, "Culte Dominical EECC", "Production Culte");
        var scene = new StudioScene("Culte Principal", "antenne");
        scene.Sources.Add(new StudioSource
        {
            Name = "Caméra 1",
            Type = SourceType.PhysicalCamera,
            RotationAngle = 180,
            FlipVertical = true,
            TextContent = "Jean 3:16",
        });

        SceneCollectionFile.Save(path, [scene]);
        var loaded = SceneCollectionFile.Load(path);

        Assert.Single(loaded);
        Assert.Equal("Culte Principal", loaded[0].Name);
        var source = Assert.Single(loaded[0].Sources);
        Assert.Equal(180, source.RotationAngle);
        Assert.True(source.FlipVertical);
        Assert.Equal("Jean 3:16", source.TextContent);
        Assert.Equal(SourceType.PhysicalCamera, source.Type);
    }

    [Fact]
    public void Le_style_la_forme_et_la_position_survivent_au_json()
    {
        var root = Path.Combine(Path.GetTempPath(), "eecc-scenes-" + Guid.NewGuid().ToString("N"));
        var path = SceneCollectionFile.PathFor(root, "Culte", "Habillage");
        var scene = new StudioScene("Culte Principal");
        var graphic = new StudioSource
        {
            Name = "Verset",
            Type = SourceType.Text,
            TextStyle = GraphicTextStyle.Verse,
            Motion = GraphicMotion.Fade,
            Shape = GraphicShape.Rounded,
            XPercent = 12,
            YPercent = 30,
            WidthPercent = 70,
            HeightPercent = 24,
            FontSize = 28,
            GraphicPlaced = true,
        };
        scene.Sources.Add(graphic);

        SceneCollectionFile.Save(path, [scene]);
        var loaded = Assert.Single(Assert.Single(SceneCollectionFile.Load(path)).Sources);

        Assert.Equal(GraphicTextStyle.Verse, loaded.TextStyle);
        Assert.Equal(GraphicMotion.Fade, loaded.Motion);
        Assert.Equal(GraphicShape.Rounded, loaded.Shape);
        Assert.Equal(12, loaded.XPercent);
        Assert.Equal(28, loaded.FontSize);
        Assert.True(loaded.GraphicPlaced);
    }

    [Fact]
    public void Un_texte_sans_position_recoit_un_bandeau()
    {
        var source = new StudioSource { Type = SourceType.Text, TextContent = "Bonjour" };
        GraphicPlacement.Ensure(source);
        Assert.True(source.GraphicPlaced);
        Assert.Equal(GraphicTextStyle.LowerThird, source.TextStyle);
        Assert.Equal(GraphicMotion.Slide, source.Motion);
        Assert.True(source.WidthPercent > 0);
        GraphicPlacement.Ensure(source);
        Assert.Equal(GraphicMotion.Slide, source.Motion);
    }
}
