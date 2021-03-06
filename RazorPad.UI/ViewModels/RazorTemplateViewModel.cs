﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Razor;
using System.Web.Razor.Parser.SyntaxTree;
using RazorPad.Compilation;
using RazorPad.Extensions;
using RazorPad.UI;
using RazorPad.UI.ModelBuilders;

namespace RazorPad.ViewModels
{
    public class RazorTemplateViewModel : ViewModelBase
    {
        private readonly ModelProviders _modelProviderFactory;
        private readonly ModelBuilders _modelBuilderFactory;
        private readonly RazorDocument _document;
        private readonly IDictionary<Type, string> _savedModels;

        public event EventHandler Executing
        {
            add { _executing += value; }
            remove { _executing -= value; }
        }
        private event EventHandler _executing;

        public ITemplateCompiler TemplateCompiler { get; set; }

        public bool AutoExecute
        {
            get { return _autoExecute; }
            set
            {
                if (_autoExecute == value)
                    return;

                _autoExecute = value;
                OnPropertyChanged("AutoExecute");
            }
        }
        private bool _autoExecute;

        public bool AutoSave
        {
            get { return _autoSave; }
            set
            {
                if (_autoSave == value)
                    return;

                _autoSave = value;
                OnPropertyChanged("AutoSave");
            }
        }
        private bool _autoSave;

        public string DisplayName
        {
            get
            {
                string displayName = "New File";

                if(Document.Metadata.ContainsKey("Title"))
                {
                    displayName = Document.Metadata["Title"];
                }
                else if (!string.IsNullOrWhiteSpace(Filename))
                {
                    try
                    {
                        displayName = new Uri(Filename).Segments.LastOrDefault();
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine("Couldn't figure out file display name from URI: " + ex);
                    }
                }

                return displayName;
            }
        }

        public RazorDocument Document
        {
            get { return _document; }
        }

        public string SelectedModelProvider
        {
            get { return _selectedModelProvider; }
            set
            {
                if (_selectedModelProvider == value)
                    return;

                _selectedModelProvider = value;
                OnPropertyChanged("SelectedModelProvider");

                UpdateModelProvider(value);
            }
        }
        private string _selectedModelProvider;

        public ObservableCollection<string> AvailableModelProviders { get; set; }

        public IModelBuilder ModelBuilder
        {
            get { return _modelBuilderFactory.Create(_document.ModelProvider); }
        }

        public ObservableTextWriter Messages
        {
            get { return _messages; }
            set
            {
                if (_messages == value)
                    return;

                _messages = value;
                OnPropertyChanged("Messages");
            }
        }
        private ObservableTextWriter _messages;

        public string ExecutedTemplateOutput
        {
            get { return _executedTemplateOutput; }
            set
            {
                if (_executedTemplateOutput == value)
                    return;

                _executedTemplateOutput = value;
                OnPropertyChanged("ExecutedTemplateOutput");
            }
        }
        private string _executedTemplateOutput;

        public string Filename
        {
            get { return _document.Filename; }
            set
            {
                if (string.Equals(_document.Filename, value, StringComparison.OrdinalIgnoreCase))
                    return;

                _document.Filename = value;
                OnPropertyChanged("Filename");
                OnPropertyChanged("DisplayName");
                OnPropertyChanged("FileDirectory");
            }
        }

        public ObservableCollection<RazorPadError> Errors
        {
            get { return _errors; }
            set
            {
                if (_errors == value)
                    return;

                _errors = value;
                OnPropertyChanged("Errors");
            }
        }
        private ObservableCollection<RazorPadError> _errors;

        public string GeneratedTemplateCode
        {
            get { return _generatedTemplateCode; }
            set
            {
                if (_generatedTemplateCode == value)
                    return;

                _generatedTemplateCode = value;
                OnPropertyChanged("GeneratedTemplateCode");
            }
        }
        private string _generatedTemplateCode;

        public GeneratorResults GeneratorResults
        {
            get { return _generatorResults; }
            set
            {
                if (_generatorResults == value)
                    return;

                _generatorResults = value;
                OnPropertyChanged("GeneratorResults");
            }
        }
        private GeneratorResults _generatorResults;

        public string Template
        {
            get { return _document.Template; }
            set
            {
                if (_document.Template == value)
                    return;

                _document.Template = value;
                OnPropertyChanged("Template");
                Refresh();
            }
        }

        public string RazorSyntaxTree
        {
            get { return _razorSyntaxTree; }
            set
            {
                if (_razorSyntaxTree == value)
                    return;

                _razorSyntaxTree = value;
                OnPropertyChanged("RazorSyntaxTree");
            }
        }
        private string _razorSyntaxTree;

        public IEnumerable<string> AssemblyReferences
        {
            get { return _document.References; }
            set
            {
                if (_document.References == value)
                    return;

                _document.References = value;
                OnPropertyChanged("AssemblyReferences");
                Refresh();
            }
        }

        public bool CanSaveToCurrentlyLoadedFile
        {
            get { return !string.IsNullOrWhiteSpace(Filename); }
        }

        public bool CanSaveAsNewFilename
        {
            get { return true; }
        }

        public bool IsDirty
        {
            get { return _isDirty; }
            private set
            {
                if (_isDirty == value)
                    return;

                _isDirty = value;
                OnPropertyChanged("IsDirty");
            }
        }
        private bool _isDirty;


        public RazorTemplateViewModel(RazorDocument document = null, ModelBuilders modelBuilderFactory = null, ModelProviders modelProviders = null)
        {
            _document = document ?? new RazorDocument();
            _modelBuilderFactory = modelBuilderFactory;
            _modelProviderFactory = modelProviders;
            _savedModels = new Dictionary<Type, string>();

            var modelProviderNames = _modelProviderFactory.Providers.Select(x => (string)new ModelProviderFactoryName(x.Value));
            AvailableModelProviders = new ObservableCollection<string>(modelProviderNames);
            _selectedModelProvider = new ModelProviderName(_document.ModelProvider);

            Errors = new ObservableCollection<RazorPadError>();
            Messages = new ObservableTextWriter();
            TemplateCompiler = new TemplateCompiler();

            AttachToModelProviderEvents(_document.ModelProvider);
        }


        public void Parse()
        {
            GeneratedTemplateCode = string.Empty;

            using (var writer = new StringWriter())
            {
                GeneratorResults = TemplateCompiler.GenerateCode(_document, writer);

                var generatedCode = writer.ToString();
                generatedCode = Regex.Replace(generatedCode, "//.*", string.Empty);
                generatedCode = Regex.Replace(generatedCode, "#.*", string.Empty);

                GeneratedTemplateCode = generatedCode.Trim();
                RazorSyntaxTree = new RazorSyntaxTreeVisualizer().Visualize(GeneratorResults.Document);
            }

            if (GeneratorResults == null || !GeneratorResults.Success)
            {
                if (GeneratorResults != null)
                {
                    var viewModels = GeneratorResults.ParserErrors.Select(x => new RazorPadRazorError(x));
                    Errors = new ObservableCollection<RazorPadError>(viewModels);
                }
            }
        }

        public void Execute()
        {
            _executing.SafeInvoke(sender: this);

            new TaskFactory().StartNew(() =>
            {
                try
                {
                    TemplateCompiler.CompilationParameters.SetReferencedAssemblies(AssemblyReferences);
                    Parse();
                    ExecutedTemplateOutput = TemplateCompiler.Execute(_document);
                }
                catch(CodeGenerationException ex)
                {
                    Dispatcher.Invoke(new Action(() => {
                        foreach (RazorError error in ex.GeneratorResults.ParserErrors)
                            Errors.Add(new RazorPadRazorError(error));
                    }));
                }
                catch (CompilationException ex)
                {
                    Dispatcher.Invoke(new Action(() => {
                        foreach (CompilerError error in ex.CompilerResults.Errors)
                            Errors.Add(new RazorPadCompilerError(error));
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(new Action(() => 
                        Errors.Add(new RazorPadError(ex)))
                    );
                }
            });
        }

        protected void Refresh()
        {
            Errors.Clear();
            Messages.Clear();
            ExecutedTemplateOutput = string.Empty;
            GeneratedTemplateCode = string.Empty;
            UpdateIsDirty();

            if (AutoExecute)
                Execute();
        }

        private void UpdateIsDirty()
        {
            // TODO: Make this better
            IsDirty = true;
        }

        private void OnRazorPadError(object sender, RazorPadErrorEventArgs e)
        {
            Dispatcher.Invoke(new Action(() => Errors.Add(e.Error)));
        }

        private void OnTemplateChanged(object sender, EventArgs args)
        {
            Refresh();
        }

        private void UpdateModelProvider(string providerName)
        {
            var newModelProvider = _modelProviderFactory.Create(providerName);
            UpdateModelProvider(newModelProvider);
        }

        private void UpdateModelProvider(IModelProvider newModelProvider)
        {
            var oldModelProvider = _document.ModelProvider;

            if (oldModelProvider != null)
                _savedModels[oldModelProvider.GetType()] = oldModelProvider.Serialize();

            DetachFromModelProviderEvents(oldModelProvider);

            _document.ModelProvider = newModelProvider;

            AttachToModelProviderEvents(newModelProvider);

            string currentlySavedModel;
            if (newModelProvider != null && _savedModels.TryGetValue(newModelProvider.GetType(), out currentlySavedModel))
            {
                newModelProvider.Deserialize(currentlySavedModel);
            }

            OnPropertyChanged("ModelBuilder");
        }

        private void AttachToModelProviderEvents(IModelProvider modelProvider)
        {
            if (modelProvider == null)
                return;

            modelProvider.Error += OnRazorPadError;
            modelProvider.ModelChanged += OnTemplateChanged;
        }

        private void DetachFromModelProviderEvents(IModelProvider oldModelProvider)
        {
            if (oldModelProvider == null) 
                return;

            oldModelProvider.ModelChanged -= OnTemplateChanged;
            oldModelProvider.Error -= OnRazorPadError;
        }
    }
}