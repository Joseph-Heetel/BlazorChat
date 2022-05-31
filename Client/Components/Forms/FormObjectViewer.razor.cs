using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;
using BlazorChat.Client;
using BlazorChat.Client.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BlazorChat.Client.Components.Forms
{

    public partial class FormObjectViewer
    {
        [Parameter]
        public JsonSchemaNode? Schema { get; set; }
        [Parameter]
        public JsonNode? Form { get; set; }
        [Parameter]
        public bool DisplayAlways { get; set; } = false;

        private bool __display = false;
        private bool _display
        {
            get => __display;
            set
            {
                __display = value;
                this.StateHasChanged();
            }
        }
        private string _title = "";
        private string _buttonIcon
        {
            get
            {
                if (_display)
                {
                    return Icons.Filled.ArrowDropUp;
                }
                else
                {
                    return Icons.Filled.ArrowDropDown;
                }
            }
        }

        protected override void OnParametersSet()
        {
            _title = "";
            if (Schema != null)
            {
                if (!string.IsNullOrEmpty(Schema!.Title))
                {
                    _title = Schema.Title;
                }
                else
                {
                    _title = Schema.PathSection;
                }
            }
        }

        private bool hasTypeConstraint(JsonSchemaNode schema, params ESchemaValueKind[] types)
        {
            return schema.GetTypeConstraint().Intersect(types).Any();
        }

        private bool hasEnumConstraint(JsonSchemaNode schema)
        {
            return schema.Constraints.ContainsKey("enum");
        }
    }
}