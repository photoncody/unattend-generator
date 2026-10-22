// Shows and hides form sections depending on the selected options.
//
// An element with data-show-if="ControlId:value1|value2" is visible only while the control
// with that id has one of the listed values (for checkboxes: "true" or "false"). Without
// JavaScript every section stays visible, and the server ignores fields of unselected modes.
"use strict";

(function () {
    const dependents = Array.from(document.querySelectorAll("[data-show-if]"));
    if (dependents.length === 0) {
        return;
    }

    function currentValue(control) {
        return control.type === "checkbox" ? String(control.checked) : control.value;
    }

    function update() {
        for (const element of dependents) {
            const [id, values] = element.dataset.showIf.split(":");
            const control = document.getElementById(id);
            if (control) {
                element.hidden = !values.split("|").includes(currentValue(control));
            }
        }
    }

    document.addEventListener("change", update);
    update();
})();
