﻿using System;
using System.IO;
using System.Linq;

using Mono.Cecil;

namespace Sharpin2 {

	public class PreSharpin {

		[Obsolete]
		public static void DumpTypes(string targetFile, string outputFile) {
			var module = ModuleDefinition.ReadModule(targetFile);
			var sw = new StreamWriter(outputFile);
			sw.WriteLine("### Type Dump for " + targetFile + " ###");
			foreach (var type in module.GetTypes()) {
				sw.WriteLine(type.FullName);
				foreach (var field in type.Fields) {
					sw.WriteLine(" - " + field.FullName);
				}
				foreach (var method in type.Methods) {
					sw.WriteLine(" * " + method.FullName);
				}
			}

			sw.Close();
			module.Dispose();
		}

		public static void ApplyAccessTransformer(string targetFile, string outputFile, string at) {
			var module = ModuleDefinition.ReadModule(targetFile);
			int lineNum = 0;
			foreach (string origLine in at.Split('\n')) {
				string line = origLine.Trim();
				lineNum++;
				if (string.IsNullOrEmpty(line) || line.StartsWith("#")) {
					continue;
				}
				// Get rid of potential EOL comments
				var parts = line.Split(new[] {'#'}, 2);
				// Now split into modifier and Type/Field/Method
				parts = parts[0].Split(new[] {' '}, 2);
				if (parts.Length != 2) {
					throw new AccessTransformerException("Broken access transformer, invalid format on line " + lineNum + ": " + line);
				}

				string modifier = parts[0].Trim();
				string target = parts[1].Trim();

				TypeDefinition type;
				FieldDefinition field = null;
				MethodDefinition method = null;
				int spaceIdx = target.IndexOf(' ');
				if (spaceIdx == -1) {
					type = module.GetType(target);
					if (type == null) {
						throw new AccessTransformerException("Broken access transformer, no Type with name '" + target + "' on line " + lineNum + ": " + line);
					}
				} else {
					string typeName = target.Substring(spaceIdx + 1, target.IndexOf(':') - spaceIdx - 1);
					type = module.GetType(typeName);
					if (type == null) {
						throw new AccessTransformerException("Broken access transformer, no Type with name '" + typeName + "' on line " + lineNum + ": " + line);
					}

					if (target.IndexOf('(') != -1) {
						method = type.Methods.SingleOrDefault(t => t.FullName == target);
						if (method == null) {
							throw new AccessTransformerException("Broken access transformer, no Method with signature '" + target + "' on line " + lineNum + ": " + line);
						}
					} else {
						field = type.Fields.SingleOrDefault(t => t.FullName == target);
						if (field == null) {
							throw new AccessTransformerException("Broken access transformer, no Field with signature '" + target + "' on line " + lineNum + ": " + line);
						}
					}
				}

				if (modifier.StartsWith("public")) {
					if (field != null) {
						field.IsPrivate = false;
						field.IsPublic = true;
					} else if (method != null) {
						method.IsPrivate = false;
						method.IsPublic = true;
					} else {
						if (type.IsNested) {
							type.IsNestedPrivate = false;
							type.IsNestedPublic = true;
						} else {
							type.IsPublic = true;
							type.IsNotPublic = false;
						}
					}
					modifier = modifier.Replace("public", "");
				}
				if (modifier.Contains("-f")) {
					if (field != null) {
						field.IsInitOnly = false;
					} else if (method != null) {
						throw new NotSupportedException("Unsealing of methods is not yet supported on line " + lineNum + ": " + line);
					} else {
						type.IsSealed = false;
					}

					modifier = modifier.Replace("-f", "");
				}

				if (modifier.Contains("+ns")) {
					if (field != null) {
						field.IsNotSerialized = true;
					} else {
						throw new AccessTransformerException("Broken access transformer, modifier +ns is only available for fields on line " + lineNum + ": " + line);
					}

					modifier = modifier.Replace("+ns", "");
					//MethodReference attributeConstructor = module.Import(typeof(NonSerializedAttribute).GetConstructor(Type.EmptyTypes));
					//Field.CustomAttributes.Add(new CustomAttribute(attributeConstructor));
				}

				if (!string.IsNullOrEmpty(modifier)) {
					throw new AccessTransformerException("Broken access transformer, invalid modifier '" + modifier + "' on line " + lineNum + ": " + line);
				}
			}

			module.Write(outputFile);
			module.Dispose();
		}
	}

}