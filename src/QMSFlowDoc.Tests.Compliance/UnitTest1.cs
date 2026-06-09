using Xunit;
using QMSFlowDoc.Web.Components.Pages;
using System;

namespace QMSFlowDoc.Tests.Compliance;

public class UnitTest1
{
    [Fact]
    public void TestBectonDickinsonBarcodeParsing()
    {
        // Arrange
        string barcode1 = "010038290555689017250831100349346";
        string barcode2 = "010038290341011817250930103262936";
        string barcodeWithBrackets = "(01)00382905556890(17)250831(10)0349346";

        // Act
        var res1 = InventoryHelpers.ParseBarcode(barcode1);
        var res2 = InventoryHelpers.ParseBarcode(barcode2);
        var resWithBrackets = InventoryHelpers.ParseBarcode(barcodeWithBrackets);

        // Assert
        Assert.True(res1.Success);
        Assert.Equal("555689", res1.Reference);
        Assert.Equal("0349346", res1.Lot);
        Assert.Equal(new DateTime(2025, 8, 31), res1.ExpirationDate);

        Assert.True(res2.Success);
        Assert.Equal("341011", res2.Reference);
        Assert.Equal("3262936", res2.Lot);
        Assert.Equal(new DateTime(2025, 9, 30), res2.ExpirationDate);

        Assert.True(resWithBrackets.Success);
        Assert.Equal("555689", resWithBrackets.Reference);
        Assert.Equal("0349346", resWithBrackets.Lot);
        Assert.Equal(new DateTime(2025, 8, 31), resWithBrackets.ExpirationDate);
    }

    [Fact]
    public void TestBioLegendBarcodeParsing()
    {
        // Arrange
        string barcode = "363030 B374784";

        // Act
        var res = InventoryHelpers.ParseBarcode(barcode);

        // Assert
        Assert.True(res.Success);
        Assert.Equal("363030", res.Reference);
        Assert.Equal("B374784", res.Lot);
        Assert.Null(res.ExpirationDate);
    }

    [Fact]
    public void TestReferenceMatching()
    {
        Assert.True(InventoryHelpers.IsReferenceMatch("555689", "555689"));
        Assert.True(InventoryHelpers.IsReferenceMatch("555689", "0555689"));
        Assert.True(InventoryHelpers.IsReferenceMatch("363030", "36-3030"));
        Assert.True(InventoryHelpers.IsReferenceMatch("b374784", "B374784"));
        Assert.False(InventoryHelpers.IsReferenceMatch("363030", "555689"));
    }

    [Fact]
    public void TestInvalidBarcodes()
    {
        var resEmpty = InventoryHelpers.ParseBarcode("");
        var resInvalid = InventoryHelpers.ParseBarcode("XYZ123");

        Assert.False(resEmpty.Success);
        Assert.False(resInvalid.Success);
    }
}
