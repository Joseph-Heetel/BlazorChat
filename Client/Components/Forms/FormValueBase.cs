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
using CustomBlazorApp.Client;
using CustomBlazorApp.Client.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using System.Text.Json.Nodes;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace CustomBlazorApp.Client.Components.Forms
{
    /// <summary>
    /// Component base representing a value view including validation
    /// </summary>
    public class FormValueBase<T> : ComponentBase
    {
        [Parameter]
        public JsonSchemaNode? Schema { get; set; } = null;
        [CascadingParameter]
        public JsonObject? FormRoot { get; set; } = null;

        private JsonObject? getWritebackNode()
        {
            if (Schema == null || FormRoot == null)
            {
                return null;
            }
            JsonNode? node = FormRoot;
            for (int i = 0; i < Schema.Path.Length - 1; i++)
            {
                string section = Schema.Path[i];
                if (node != null && node is JsonObject obj)
                {
                    node = obj[section];
                }
            }
            return node as JsonObject;
        }
        protected bool _paramsValid = false;

        protected T? _nodeValue
        {
            get
            {
                var writebackNode = getWritebackNode();
                if (writebackNode != null && Schema != null && writebackNode[Schema.PathSection] is JsonValue jsonValue)
                {
                    return jsonValue.GetValue<T?>();
                }
                return default(T?);
            }
            set
            {
                var writebackNode = getWritebackNode();
                if (writebackNode != null && Schema != null)
                {
                    writebackNode[Schema.PathSection] = JsonValue.Create<T>(value);
                }
            }
        }

        protected List<string> _errorHints = new List<string>();

        protected string? _errorHint
        {
            get
            {
                if (_errorHints.Any())
                {
                    return string.Join(" ", _errorHints);
                }
                return null;
            }
        }

        protected T? __value = default(T?);
        protected T? _value
        {
            get
            {
                return __value;
            }
            set
            {
                this.__value = value;
                _errorHints.Clear();
                _nodeValue = value;
                validateValue(value);
                this.StateHasChanged();
            }
        }

        protected bool _optional { get => Schema?.IsOptional ?? false; }

        protected override void OnParametersSet()
        {
            _paramsValid = Schema != null && FormRoot != null;

            if (_paramsValid && __value is null)
            {
                __value = _nodeValue;
            }
            this.StateHasChanged();
        }
        protected virtual bool validateValue(T? value)
        {
            var writebackNode = getWritebackNode();
            if (Schema == null || writebackNode == null)
            {
                return false;
            }
            var result = Schema.ValidateSelf(writebackNode[Schema.PathSection]);
            _errorHints.Clear();
            _errorHints.AddRange(result.ErrorHints);
            if (!result.IsSuccess && _errorHints.Count == 0)
            {
                _errorHints.Add("Unknown Validation Error");
            }
            return result.IsSuccess;
        }
    }
}