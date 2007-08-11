#region Header
/**
 * JsonWriter.cs
 *   Stream-like facility to output JSON text.
 *
 * The authors disclaim copyright to this source code. For more details, see
 * the COPYING file included with this distribution.
 **/
#endregion


using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;


namespace LitJson
{
    internal enum Condition
    {
        InArray,
        InObject,
        NotAProperty,
        Property,
        Value
    }

    internal class WriterContext
    {
        public int  Count;
        public bool InArray;
        public bool InObject;
        public bool ExpectingValue;
        public int  Padding;
    }

    public class JsonWriter
    {
        #region Fields
        private static NumberFormatInfo number_format;

        private WriterContext        context;
        private Stack<WriterContext> ctx_stack;
        private bool                 has_reached_end;
        private char[]               hex_seq;
        private int                  indentation;
        private int                  indent_value;
        private bool                 pretty_print;
        private bool                 validate;
        private TextWriter           writer;
        #endregion


        #region Properties
        public int IndentValue {
            get { return indent_value; }
            set {
                indentation = (indentation / indent_value) * value;
                indent_value = value;
            }
        }

        public bool PrettyPrint {
            get { return pretty_print; }
            set { pretty_print = value; }
        }

        public TextWriter TextWriter {
            get { return writer; }
        }

        public bool Validate {
            get { return validate; }
            set { validate = value; }
        }
        #endregion


        #region Constructors
        static JsonWriter ()
        {
            number_format = NumberFormatInfo.InvariantInfo;
        }

        public JsonWriter ()
        {
            writer = new StringWriter ();

            Init ();
        }

        public JsonWriter (StringBuilder sb) :
            this (new StringWriter (sb))
        {
        }

        public JsonWriter (TextWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException ("writer");

            this.writer = writer;

            Init ();
        }
        #endregion


        #region Private Methods
        private void Init ()
        {
            has_reached_end = false;
            hex_seq = new char[4];
            indentation = 0;
            indent_value = 4;
            pretty_print = false;
            validate = true;

            ctx_stack = new Stack<WriterContext> ();
            context = new WriterContext ();
            ctx_stack.Push (context);
        }

        private static void IntToHex (int n, char[] hex)
        {
            int num;

            for (int i = 0; i < 4; i++) {
                num = n % 16;

                if (num < 10)
                    hex[3 - i] = (char) ('0' + num);
                else
                    hex[3 - i] = (char) ('A' + (num - 10));

                n >>= 4;
            }
        }

        private void Check (Condition cond)
        {
            if (! context.ExpectingValue)
                context.Count++;

            if (! validate)
                return;

            if (has_reached_end)
                throw new JsonException (
                    "A complete JSON symbol has already been written");

            switch (cond) {
            case Condition.InArray:
                if (! context.InArray)
                    throw new JsonException (
                        "Can't close an array here");
                break;

            case Condition.InObject:
                if (! context.InObject || context.ExpectingValue)
                    throw new JsonException (
                        "Can't close an object here");
                break;

            case Condition.NotAProperty:
                if (context.InObject && ! context.ExpectingValue)
                    throw new JsonException (
                        "Expected a property");
                break;

            case Condition.Property:
                if (! context.InObject || context.ExpectingValue)
                    throw new JsonException (
                        "Can't add a property here");
                break;

            case Condition.Value:
                if (! context.InArray &&
                    (! context.InObject || ! context.ExpectingValue))
                    throw new JsonException (
                        "Can't add a value here");

                break;
            }
        }

        private void Indent ()
        {
            if (pretty_print)
                indentation += indent_value;
        }


        private void Put (string str)
        {
            if (pretty_print && ! context.ExpectingValue)
                for (int i = 0; i < indentation; i++)
                    writer.Write (' ');

            writer.Write (str);
        }

        private void PutNewline ()
        {
            PutNewline (true);
        }

        private void PutNewline (bool add_comma)
        {
            if (add_comma && ! context.ExpectingValue &&
                context.Count > 1)
                writer.Write (',');

            if (pretty_print && ! context.ExpectingValue)
                writer.Write ('\n');
        }

        private void PutString (string str)
        {
            Put (String.Empty);

            writer.Write ('"');

            int n = str.Length;
            for (int i = 0; i < n; i++) {
                switch (str[i]) {
                case '\n':
                    writer.Write ("\\n");
                    continue;

                case '\r':
                    writer.Write ("\\r");
                    continue;

                case '\t':
                    writer.Write ("\\t");
                    continue;

                case '"':
                case '\\':
                    writer.Write ('\\');
                    writer.Write (str[i]);
                    continue;

                case '\f':
                    writer.Write ("\\f");
                    continue;

                case '\b':
                    writer.Write ("\\b");
                    continue;
                }

                if ((int) str[i] >= 32 && (int) str[i] <= 126) {
                    writer.Write (str[i]);
                    continue;
                }

                // Default, turn into a \uXXXX sequence
                IntToHex ((int) str[i], hex_seq);
                writer.Write ("\\u");
                writer.Write (hex_seq);
            }

            writer.Write ('"');
        }

        private void Unindent ()
        {
            if (pretty_print)
                indentation -= indent_value;
        }
        #endregion


        public override string ToString ()
        {
            if (! (writer is StringWriter))
                return String.Empty;

            return ((StringWriter) writer).ToString ();
        }

        public void Write (bool boolean)
        {
            Check (Condition.Value);
            PutNewline ();

            Put (boolean ? "true" : "false");

            context.ExpectingValue = false;
        }

        public void Write (decimal number)
        {
            Check (Condition.Value);
            PutNewline ();

            Put (Convert.ToString (number, number_format));

            context.ExpectingValue = false;
        }

        public void Write (double number)
        {
            Check (Condition.Value);
            PutNewline ();

            string str = Convert.ToString (number, number_format);
            Put (str);

            if (str.IndexOf ('.') == -1 &&
                str.IndexOf ('E') == -1)
                writer.Write (".0");

            context.ExpectingValue = false;
        }

        public void Write (int number)
        {
            Check (Condition.Value);
            PutNewline ();

            Put (Convert.ToString (number, number_format));

            context.ExpectingValue = false;
        }

        public void Write (long number)
        {
            Check (Condition.Value);
            PutNewline ();

            Put (Convert.ToString (number, number_format));

            context.ExpectingValue = false;
        }

        public void Write (string str)
        {
            Check (Condition.Value);
            PutNewline ();

            if (str == null)
                Put ("null");
            else
                PutString (str);

            context.ExpectingValue = false;
        }

        public void WriteArrayEnd ()
        {
            Check (Condition.InArray);
            PutNewline (false);

            ctx_stack.Pop ();
            if (ctx_stack.Count == 1)
                has_reached_end = true;
            else {
                context = ctx_stack.Peek ();
                context.ExpectingValue = false;
            }

            Unindent ();
            Put ("]");
        }

        public void WriteArrayStart ()
        {
            Check (Condition.NotAProperty);
            PutNewline ();

            Put ("[");

            context = new WriterContext ();
            context.InArray = true;
            ctx_stack.Push (context);

            Indent ();
        }

        public void WriteObjectEnd ()
        {
            Check (Condition.InObject);
            PutNewline (false);

            ctx_stack.Pop ();
            if (ctx_stack.Count == 1)
                has_reached_end = true;
            else {
                context = ctx_stack.Peek ();
                context.ExpectingValue = false;
            }

            Unindent ();
            Put ("}");
        }

        public void WriteObjectStart ()
        {
            Check (Condition.NotAProperty);
            PutNewline ();

            Put ("{");

            context = new WriterContext ();
            context.InObject = true;
            ctx_stack.Push (context);

            Indent ();
        }

        public void WritePropertyName (string property_name)
        {
            Check (Condition.Property);
            PutNewline ();

            PutString (property_name);

            if (pretty_print) {
                if (property_name.Length > context.Padding)
                    context.Padding = property_name.Length;

                for (int i = context.Padding - property_name.Length;
                     i >= 0; i--)
                    writer.Write (' ');

                writer.Write (": ");
            } else
                writer.Write (':');

            context.ExpectingValue = true;
        }
    }
}
