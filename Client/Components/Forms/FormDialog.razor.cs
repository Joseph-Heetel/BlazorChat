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
using BlazorChat.Shared;

namespace BlazorChat.Client.Components.Forms
{
    public partial class FormDialog
    {
        public enum EState
        {
            Fetching,
            Invalid,
            Editing,
            Uploading,
            Submitted,
        }

        //private EState State;
        [CascadingParameter]
        MudDialogInstance MudDialog { get; set; } = new MudDialogInstance();
        [Parameter]
        public FormRequest? FormRequest { get; set; }

        private EState _state = EState.Fetching;
        private JsonObject? _formRoot = null;
        private JsonSchemaNode? _schema;
        private ValidationResult _validation = new ValidationResult();
        private string _errors = "";

        protected override async Task OnParametersSetAsync()
        {
            _formRoot = null;
            _schema = null;
            if (FormRequest != null)
            {
                using (JsonDocument? doc = await Api.GetForm(FormRequest.FormId))
                {
                    if (doc == null)
                    {
                        _state = EState.Invalid;
                    }
                    else
                    {
                        _state = EState.Editing;
                    }
                    _schema = doc?.Deserialize<JsonSchemaNode>();
                    if (_schema != null)
                    {
                        _schema.Init(doc!.RootElement, new SchemaConstraintParser(), false);
                        if (_schema.Properties.Count > 0)
                        {
                            _formRoot = _schema.Generate() as JsonObject;
                        }
                    }
                }
            }
            this.StateHasChanged();
        }

        private async void Submit()
        {
            Validate();
            if (FormRequest != null && _formRoot != null && _validation.IsSuccess)
            {
                _state = EState.Uploading;
                Console.WriteLine($"Submitting: {JsonSerializer.Serialize(_formRoot)}");
                await Api.PostFormResponse(FormRequest.Id, _formRoot);
                MudDialog?.Close();
            }
        }

        private void Validate()
        {
            if (_schema == null)
            {
                return;
            }

            _validation = _schema.ValidateRecursively(_formRoot);
            switch (_validation.ErrorHints.Count)
            {
                case 0:
                    _errors = "";
                    break;
                case 1:
                    _errors = "1 Error";
                    break;
                default:
                    _errors = $"{_validation.ErrorHints.Count} Errors";
                    break;
            }
            this.StateHasChanged();
        }
    }
}