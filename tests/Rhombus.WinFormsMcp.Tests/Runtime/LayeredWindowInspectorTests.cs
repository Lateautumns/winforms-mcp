using System.Text.Json;

using Rhombus.WinFormsMcp.RuntimeBridge.Inspection;
using Rhombus.WinFormsMcp.RuntimeContracts;

namespace Rhombus.WinFormsMcp.Tests.Runtime;

[TestFixture]
public sealed class LayeredWindowInspectorTests {
    [TestCase("AntdUI.LayeredFormSelectDown", "select-dropdown")]
    [TestCase("AntdUI.LayeredFormSelectMultipleCheck", "select-dropdown")]
    [TestCase("AntdUI.LayeredFormMenuDown", "menu-popup")]
    [TestCase("AntdUI.LayeredFormContextMenuStrip", "context-menu")]
    [TestCase("AntdUI.LayeredFormDatePicker", "date-picker")]
    [TestCase("AntdUI.LayeredFormTimePicker", "time-picker")]
    [TestCase("AntdUI.TooltipForm", "tooltip")]
    [TestCase("AntdUI.LayeredFormModal", "modal")]
    [TestCase("AntdUI.LayeredFormDrawer", "drawer")]
    [TestCase("AntdUI.MessageFrm", "message")]
    [TestCase("AntdUI.NotificationFrm", "notification")]
    public void ClassifySemanticType_UsesStableProviderVocabulary(string runtimeType, string expected) {
        Assert.That(LayeredWindowInspector.ClassifySemanticType(runtimeType), Is.EqualTo(expected));
    }

    [Test]
    public void ProviderWindowMetadata_IsOptionalAndUsesCamelCaseWireFields() {
        var snapshot = new WindowSnapshot {
            Hwnd = "0x10",
            ProviderWindowMetadata = new ProviderWindowMetadataSnapshot {
                Provider = "AntdUI",
                RuntimeWindowType = "AntdUI.LayeredFormSelectDown",
                SemanticType = "select-dropdown",
                OwnerControlId = "ctrl_1",
                Items = [
                    new ProviderWindowItemSnapshot {
                        Index = 0,
                        Text = "Alpha",
                        Selected = true
                    }
                ],
                SelectedItem = new ProviderWindowItemSnapshot {
                    Index = 0,
                    Text = "Alpha",
                    Selected = true
                }
            }
        };

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Multiple(() => {
            Assert.That(json, Does.Contain("providerWindowMetadata"));
            Assert.That(json, Does.Contain("runtimeWindowType"));
            Assert.That(json, Does.Contain("ownerControlId"));
            Assert.That(json, Does.Contain("selectedItem"));
        });

        var withoutMetadata = JsonSerializer.Serialize(
            new WindowSnapshot { Hwnd = "0x11" },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.That(withoutMetadata, Does.Not.Contain("providerWindowMetadata"));
    }

    [Test]
    public void ProviderWindowItemSnapshot_PreservesBoundedStateAndRange() {
        var metadata = new ProviderWindowMetadataSnapshot {
            SemanticType = "menu-popup",
            Truncated = true,
            VisibleRange = new ProviderWindowRangeSnapshot {
                Start = 2,
                Count = 3,
                TotalCount = 20
            },
            Warnings = ["popup changed during inspection"]
        };

        Assert.Multiple(() => {
            Assert.That(metadata.Truncated, Is.True);
            Assert.That(metadata.VisibleRange?.Start, Is.EqualTo(2));
            Assert.That(metadata.VisibleRange?.Count, Is.EqualTo(3));
            Assert.That(metadata.VisibleRange?.TotalCount, Is.EqualTo(20));
            Assert.That(metadata.Warnings, Has.One.Items);
        });
    }
}