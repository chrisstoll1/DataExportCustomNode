let $selectedField;
let cachedExportPath = {
    connectionString: "",
    exportPath: "",
    initial: true
}

//Enable sorting on both tables
$('#mappingBody').sortable({handle: '#handle', containment: "#mappingBody"});
$('#replacementBody').sortable({
    handle: '#replacementHandle', 
    containment: "#replacementBody",
    update: function(event, ui) {
        rebuildOptionsJson();
    }
});

//Setup
cacheExportPathValue();
refreshLabels();

//On field mapping add row clicked
//Add new row for field mapping table
$(".addRow").click(function() {
	var allListElements = document.querySelectorAll("[repeat-list]");
	var lastListElement = allListElements[allListElements.length - 1];

	lastListElement.parentNode.appendChild( lastListElement.cloneNode(true) );
	
	var newListElements = document.querySelectorAll("[repeat-list]");
	var newListElement = newListElements[newListElements.length - 1];
	$(newListElement).find("#fieldSelect").val("");
	$(newListElement).find("#columnName").val("");
});

//On replacement add row clicked
$(".replacementAddRow").click(function() {
    replacementAddRow();
    replacementTableUpdated();
});

//On replacement remove row clicked
//Remove row from replacement table
$("body").on("click", "#replacementRemoveRow", function () {
    if ($("#replacementBody").children().length > 1){
        $(this).closest("tr").remove();
        rebuildOptionsJson();
    }
    replacementTableUpdated();
});

//On field mapping remove row clicked
//Remove row from field mapping table
$("body").on("click", "#removeRow", function () {
    if ($("#mappingBody").children().length > 1){
        $(this).closest("tr").remove();
    }
});

//On field select drop-down change
//Set column name default value
$("body").on("change", "#fieldSelect", function () {
    $(this).closest("tr").find("input").val(this[this.selectedIndex].label);
    console.log(this);
});

//On export type change
//Refresh labels
$('input[type=radio][name=exportTypeRadio]').change(function() {
    refreshLabels();
});

//On field options clicked
//Field options dropdown UI logic
$("body").on("click", "#optionDropDown", function () {
    if ($("#mappingBody").children().length == 1){
        $(this).closest("div").find("ul").children().last().attr('class', 'disabled');
        $(this).closest("div").find("ul").children().last().find("a").attr('id', '');
    }else{
        $(this).closest("div").find("ul").children().last().attr('class', '');
        $(this).closest("div").find("ul").children().last().find("a").attr('id', 'removeRow');
    }
    var fieldSelectValue = $(this).closest("tr").find("#fieldSelect").val();
    if (fieldSelectValue == '' || fieldSelectValue == 'DocID' || fieldSelectValue == 'ArchiveID' || fieldSelectValue == 'IID' || fieldSelectValue == '-RowCounter-') {
        $(this).closest("div").find("ul").children().first().attr('class', 'disabled');
        $(this).closest("div").find("ul").children().first().find("a").attr('id', '');
        $(this).closest("div").find("ul").children().first().find("a").attr('data-target', '');
    }else{
        $(this).closest("div").find("ul").children().first().attr('class', '');
        $(this).closest("div").find("ul").children().first().find("a").attr('id', 'openRowOptions');
        $(this).closest("div").find("ul").children().first().find("a").attr('data-target', '#fieldOptions');
    }
});

//On open row options clicked
//Load options from field
$("body").on("click", "#openRowOptions", function () {
    $("#format").val("");
    $("#replacementBody").children().each(function () {
        $(this).closest("tr").find("#patternMatch").val("");
        $(this).closest("tr").find("#replacement").val("");
        if ($("#replacementBody").children().length > 1){
            $(this).closest("tr").remove();
        }
    });

    $selectedField = $(this).closest("tr");
    var fieldName = $selectedField.find("#fieldSelect").val();
    $('#modalLabel').text(`${fieldName} - Options`);

    var jsonData; 
    try {
        jsonData = JSON.parse($selectedField.find("#options").val());
    } catch (error) {
        jsonData = {
            "format": "",
            "replacements": []
        };
        $selectedField.find("#options").val(JSON.stringify(jsonData));
    }

    $("#format").val(jsonData.format);
    for (let i = 0; i < jsonData.replacements.length; i++) {
        if (i == 0) {
            $("#replacementBody").children().eq(0).find("#patternMatch").val(jsonData.replacements[i].p);
            $("#replacementBody").children().eq(0).find("#replacement").val(jsonData.replacements[i].r);
        }else{
            replacementAddRow(jsonData.replacements[i].p, jsonData.replacements[i].r);
        }
    } 

    replacementTableUpdated();
});

//On options field change
$("body").on("change", "#replacementBody, #format", function () {
    rebuildOptionsJson();
});

//Disable Remove Row Button When only 1 row remains
function replacementTableUpdated(){
    if ($("#replacementBody").children().length == 1){
        $("#replacementRemoveRow").addClass("removeRowDisabled");
    }else{
        $("#replacementBody").children().each(function () {
            $(this).closest("tr").find("#replacementRemoveRow").removeClass("removeRowDisabled");
        });
    }
}

//Rebuilds the options object and sets it to the fields option setting
function rebuildOptionsJson() {
    var jsonData = {
        "format": $("#format").val(),
        "replacements": []
    };
    $("#replacementBody").children().each(function () {
        var replacementRow = {
            "p": $(this).find("#patternMatch").val(),
            "r": $(this).find("#replacement").val()
        };
        jsonData.replacements.push(replacementRow);
    });
    $selectedField.find("#options").val(JSON.stringify(jsonData));
    // console.log(JSON.parse($selectedField.find("#options").val()));
}

//Add new row for replacement table
function replacementAddRow(patterMatch = "", replacement = "") {
	var allListElements = document.querySelectorAll("[replacement-repeat-list]");
	var lastListElement = allListElements[allListElements.length - 1];

	lastListElement.parentNode.appendChild( lastListElement.cloneNode(true) );
	
	var newListElements = document.querySelectorAll("[replacement-repeat-list]");
	var newListElement = newListElements[newListElements.length - 1];
	$(newListElement).find("#patternMatch").val(patterMatch);
	$(newListElement).find("#replacement").val(replacement);
}

//Cache the loaded in export path
function cacheExportPathValue(){
    if ($("#csvRadio").prop('checked')){
        cachedExportPath.exportPath = $("#exportPath").val(); 
    }else if ($("#sqlRadio").prop('checked')){
        cachedExportPath.connectionString = $("#exportPath").val();
    }
}

//Show/Hide UI components, change labels, load cached values
function refreshLabels() {
    if ($("#csvRadio").prop('checked')){
        if (!cachedExportPath.initial){
            cachedExportPath.connectionString = $("#exportPath").val();
        }
        $("#exportPath").val(cachedExportPath.exportPath);
       
        $("#exportPathLabel").text("Export Path");
        $(".checkbox-wrapper").show();
        $(".tablename-wrapper").hide();
        $(".delimiter-wrapper").show();
    }else if ($("#sqlRadio").prop('checked')){
        if (!cachedExportPath.initial){
            cachedExportPath.exportPath = $("#exportPath").val();
        }
        $("#exportPath").val(cachedExportPath.connectionString);

        $("#exportPathLabel").text("Connection String");
        $(".checkbox-wrapper").hide();
        $(".tablename-wrapper").show();
        $(".delimiter-wrapper").hide();
    }
    cachedExportPath.initial = false;
}

